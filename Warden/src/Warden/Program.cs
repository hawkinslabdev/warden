using System.IO.Compression;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.FileProviders;
using Serilog;
using System.Threading.RateLimiting;
using Warden.Configuration;
using Warden.Endpoints;
using Warden.Models;
using Warden.Serialization;
using Warden.Services;
using Warden.Services.Admin;
using Warden.Services.MarkdownExtensions;

Directory.CreateDirectory("log");

var cliArgs = CliArguments.Parse(args);
var exportDir = cliArgs.ExportDir;
var exportBaseUrl = cliArgs.ExportBaseUrl;
var isExport = exportDir is not null;

try
{
    var builder = WebApplication.CreateBuilder(args);
    var config = builder.Configuration;

    Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(config).CreateLogger();
    builder.Host.UseSerilog();

    // docs
    var docsOptions = config.GetSection("Docs").Get<DocsOptions>() ?? new DocsOptions();
    var basePath = NormalizeBasePath(cliArgs.BasePath ?? docsOptions.BasePath);
    docsOptions = docsOptions with
    {
        RootPath = Environment.GetEnvironmentVariable("DOCS_ROOT_PATH") ?? docsOptions.RootPath,
        BasePath = basePath,
        EnableHotReload = docsOptions.EnableHotReload && !isExport,
    };
    builder.Services.AddSingleton(docsOptions);

    var docsRootAbsolute = Path.GetFullPath(docsOptions.RootPath).Replace(Path.DirectorySeparatorChar, '/');
    var webRootPath = builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");

    // monitoring
    var monitoringOptions = config.GetSection("Monitoring").Get<MonitoringOptions>() ?? new MonitoringOptions();
    if (config["DatabasePath"] is { Length: > 0 } databasePathAlias)
        monitoringOptions = monitoringOptions with { DatabasePath = databasePathAlias };
    builder.Services.AddSingleton(monitoringOptions);
    builder.Services.AddSingleton<HeartbeatStore>();
    builder.Services.AddSingleton<AdminOverrideStore>();

    var dbDir = Path.GetDirectoryName(Path.GetFullPath(monitoringOptions.DatabasePath, AppContext.BaseDirectory))!;
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dbDir, "keys")));

    builder.Services.AddHttpClient(MonitorScheduler.HttpClientName, ConfigureMonitorClient);
    builder.Services.AddHttpClient(MonitorScheduler.InsecureHttpClientName, ConfigureMonitorClient)
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

    // git sync
    var gitSyncOptions = new GitSyncOptions
    {
        Enabled = Environment.GetEnvironmentVariable("GIT_ENABLED") is "true" or "1",
        Url = Environment.GetEnvironmentVariable("GIT_URL"),
        Username = Environment.GetEnvironmentVariable("GIT_USERNAME"),
        Password = Environment.GetEnvironmentVariable("GIT_PASSWORD"),
        Root = Environment.GetEnvironmentVariable("GIT_ROOT"),
        Cron = Environment.GetEnvironmentVariable("GIT_CRON")?.Trim().Trim('"', '\'') ?? "*/5 * * * *",
    };
    var usingGitRoot = !string.IsNullOrWhiteSpace(gitSyncOptions.Root);
    var gitRoot = usingGitRoot
        ? Path.GetFullPath(gitSyncOptions.Root!).Replace(Path.DirectorySeparatorChar, '/')
        : docsRootAbsolute;

    if (!isExport)
        builder.Services.AddHostedService(sp => new GitContentSyncService(
            gitSyncOptions, gitRoot, sp.GetRequiredService<ILogger<GitContentSyncService>>()));

    // admin
    var authOptions = AuthOptions.FromEnvironment() with
    {
        IsExport = isExport,
        IncidentPath = AuthOptions.ResolveIncidentPath(gitRoot, docsRootAbsolute),
    };
    builder.Services.AddSingleton(authOptions);

    if (!string.IsNullOrEmpty(authOptions.Issuer) && !authOptions.Enabled)
        Log.Warning("OIDC_ISSUER is set but the admin panel stays off: the issuer must be an absolute https URL (or http on loopback), both OIDC_CLIENT_ID and OIDC_CLIENT_SECRET must be present, and OIDC_ALLOWED_SUBJECTS must list at least one subject (an empty allowlist would admit every account the issuer serves)");

    var adminEnabled = authOptions.AdminEnabled;
    if (adminEnabled)
    {
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(opts =>
            {
                opts.Cookie.Name = "warden_admin";
                opts.Cookie.HttpOnly = true;
                opts.Cookie.SameSite = SameSiteMode.Lax;
                opts.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                opts.LoginPath = $"{authOptions.AuthPath}/login";
                opts.SlidingExpiration = true;
                opts.ExpireTimeSpan = TimeSpan.FromHours(12);
            })
            .AddOpenIdConnect(opts =>
            {
                opts.Authority = authOptions.Issuer;
                opts.ClientId = authOptions.ClientId;
                opts.ClientSecret = authOptions.ClientSecret;
                opts.ResponseType = "code";
                opts.UsePkce = true;
                opts.RequireHttpsMetadata = authOptions.RequiresHttpsMetadata;
                opts.SaveTokens = false;
                opts.Scope.Clear();
                opts.Scope.Add("openid");
                opts.CallbackPath = $"{authOptions.AuthPath}/callback";
                opts.SignedOutCallbackPath = $"{authOptions.AuthPath}/signed-out";
                opts.RemoteSignOutPath = $"{authOptions.AuthPath}/signout";
                opts.GetClaimsFromUserInfoEndpoint = false;
            });

        builder.Services.AddAntiforgery(opts => opts.Cookie.Name = "warden_admin_af");
        builder.Services.AddSingleton<AdminConfigWriter>();

        // allowlist checked per request
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AuthEndpoints.AdminPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(ctx => authOptions.Permits(
                    ctx.User.FindFirst(AuthEndpoints.SubjectClaim)?.Value
                    ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value)));
    }

    // proxy
    var proxyOptions = config.GetSection("Proxy").Get<ProxyOptions>() ?? new ProxyOptions();
    builder.Services.Configure<ForwardedHeadersOptions>(options => ForwardedHeaderSetup.Configure(options, proxyOptions));

    if (adminEnabled && !proxyOptions.TrustAny && proxyOptions.Trusted.Length == 0 && !authOptions.IsLoopbackIssuer)
        Log.Warning("The admin panel is on but no Proxy:Trusted entry is configured. If TLS terminates at a reverse proxy, Warden cannot see that the request was https, so the admin session cookie ships without the Secure flag and no HSTS header is sent. Set Proxy:Trusted to your proxy's address or CIDR, or terminate TLS in Warden itself");

    // altcha
    var altchaOptions = config.GetSection("Altcha").Get<AltchaOptions>() ?? new AltchaOptions();
    if (altchaOptions.Enabled)
        builder.Services.AddAltchaGate(altchaOptions);

    // theme
    var themeDir = Path.Combine(usingGitRoot ? gitRoot : webRootPath, "theme");
    try { Directory.CreateDirectory(themeDir); }
    catch (IOException ex) { Log.Debug(ex, "Could not create theme directory {ThemeDir}", themeDir); }

    var themeOptions = config.GetSection("Docs:Themes").Get<ThemeOptions>()
        ?? ThemeJsonLoader.Load(themeDir)
        ?? new ThemeOptions();
    builder.Services.AddSingleton(themeOptions);

    // code group icons
    var codeGroupIconOptions = config.GetSection("Docs:CodeGroupIcons").Get<CodeGroupIconOptions>() ?? new CodeGroupIconOptions();
    if (codeGroupIconOptions.Enabled && codeGroupIconOptions.BaseUrl.StartsWith('/') && !codeGroupIconOptions.BaseUrl.StartsWith("//"))
    {
        var available = ScanIconSlugs(Path.Combine(webRootPath, codeGroupIconOptions.BaseUrl.Trim('/')), codeGroupIconOptions.Format);
        if (available.Count > 0)
            codeGroupIconOptions = codeGroupIconOptions with { Available = available };
    }
    builder.Services.AddSingleton(codeGroupIconOptions);

    // rendering
    builder.Services.AddSingleton<ISyntaxHighlighter, TextMateSyntaxHighlighter>();
    builder.Services.AddSingleton<MathRenderer>();
    builder.Services.AddSingleton(sp => new MarkdownService(
        sp.GetRequiredService<ISyntaxHighlighter>(), basePath,
        sp.GetRequiredService<CodeGroupIconOptions>(),
        sp.GetRequiredService<MathRenderer>(),
        sp.GetRequiredService<ILogger<MarkdownService>>()));
    builder.Services.AddSingleton<ContentService>();

    // content first so scheduler sees config
    builder.Services.AddHostedService(sp => sp.GetRequiredService<ContentService>());
    builder.Services.AddHostedService<MonitorScheduler>();

    var customCsp = config["Docs:ContentSecurityPolicy"] is { } csp && !string.IsNullOrWhiteSpace(csp) ? csp : null;

    builder.Services.AddSingleton(new PageRequestSettings(
        BasePath: basePath,
        CustomCsp: customCsp,
        ThemeDir: themeDir,
        WebRootPath: webRootPath,
        DocsRootAbsolute: docsRootAbsolute,
        PublicBaseUrl: PageRequestSettings.ResolvePublicBaseUrl(exportBaseUrl, docsOptions.PublicBaseUrl, config["PublicBaseUrl"]),
        CliTheme: cliArgs.Theme,
        CliStructure: cliArgs.Structure));
    builder.Services.AddSingleton<PageResponder>();
    builder.Services.AddSingleton<PageRequestHandler>();

    // http
    builder.Services.ConfigureHttpJsonOptions(opts =>
        opts.SerializerOptions.TypeInfoResolverChain.Insert(0, WardenJsonContext.Default));

    builder.Services.AddResponseCompression(opts =>
    {
        opts.EnableForHttps = true;
        opts.Providers.Add<BrotliCompressionProvider>();
        opts.Providers.Add<GzipCompressionProvider>();
    });
    builder.Services.Configure<BrotliCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);
    builder.Services.Configure<GzipCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Fastest);

    builder.WebHost.ConfigureKestrel(KestrelHardening.Configure);

    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy(RateLimitPolicies.Api, PerIp(30));
        options.AddPolicy(RateLimitPolicies.Auth, PerIp(30));
        options.AddPolicy(RateLimitPolicies.Admin, PerIp(120));
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    LogApplicationBanner();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment() && app.Services.GetRequiredService<PageRequestSettings>().PublicBaseUrl is null)
        Log.Warning("Docs:PublicBaseUrl is not set; canonical URLs, feeds and robots.txt are built from the caller's Host header. Set it in production.");

    // before content service renders
    await app.Services.GetRequiredService<ISyntaxHighlighter>().InitializeAsync(CancellationToken.None);

    app.UseExceptionHandler(errorApp => errorApp.Run(async ctx =>
    {
        var error = ctx.Features.Get<IExceptionHandlerPathFeature>()?.Error;
        Log.Error(error, "Unhandled exception for {Path}", ctx.Request.Path);
        await ctx.RequestServices.GetRequiredService<PageResponder>().Write500Async(ctx);
    }));

    app.UseForwardedHeaders();

    if (basePath.Length > 0)
        app.UsePathBase(basePath);

    app.UseSecurityHeaders(customCsp);
    app.UseResponseCompression();

    // static files
    var defaultWebRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot-default");
    if (Directory.Exists(defaultWebRoot) && Directory.Exists(webRootPath))
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new CompositeFileProvider(new PhysicalFileProvider(webRootPath), new PhysicalFileProvider(defaultWebRoot))
        });
    else
        app.UseStaticFiles();

    if (usingGitRoot)
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(themeDir), RequestPath = "/theme" });

    var assetsDir = Path.Combine(Path.GetFullPath(docsOptions.RootPath), "assets");
    AssetVersioning.Current = new AssetVersioning(assetsDir);
    if (Directory.Exists(assetsDir))
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(assetsDir),
            RequestPath = "/assets",
            ContentTypeProvider = AssetContentTypes.Provider(),
            ServeUnknownFileTypes = false,
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.CacheControl =
                    !app.Environment.IsDevelopment() && ctx.Context.Request.Query.ContainsKey("v")
                        ? "public,max-age=31536000,immutable"
                        : "no-cache";

                // svg can run inline script
                if (ctx.File.Name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    ctx.Context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
            }
        });

    // pipeline
    if (altchaOptions.Enabled && !isExport)
        app.UseAltchaGate(adminEnabled ? authOptions.AuthPath : null, adminEnabled ? authOptions.AdminPath : null);

    app.UseRouting();

    if (adminEnabled)
    {
        app.UseAuthentication();
        app.UseAuthorization();
    }

    app.UseRateLimiter();

    // endpoints
    if (altchaOptions.Enabled)
        app.MapAltchaEndpoints();

    if (adminEnabled)
    {
        app.UseAntiforgery();
        app.MapAuthEndpoints(authOptions);
        app.MapAdminEndpoints(authOptions);
    }

    app.MapHealthEndpoints();
    app.MapApiEndpoints();
    app.MapSeoEndpoints();
    app.MapStatusEndpoints();
    app.MapAssetEndpoints();
    app.MapContentEndpoints();

    if (exportDir is not null)
    {
        await StaticSiteExporter.RunAsync(app, exportDir, exportBaseUrl, CancellationToken.None);
        Log.Information("Static export written to {Dir}", exportDir);
        return;
    }

    // run
    var urls = app.Urls.Count > 0
        ? [.. app.Urls]
        : (Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? config["urls"] ?? "http://localhost:5000").Split(';');

    if (!PortAvailabilityChecker.TryEnsureUrlsAvailable(urls, out var conflictingPort))
    {
        Log.Fatal("Port {Port} is already in use. Stop the existing process and try again.", conflictingPort);
        return;
    }

    Log.Information("Application is hosted on the following URLs:");
    foreach (var url in urls)
    {
        Log.Information("   {Url}", url.Trim());
        Log.Information("");
    }

    app.Lifetime.ApplicationStarted.Register(() =>
        Log.Information("Admin panel: {State}", adminEnabled ? $"enabled; auth issuer: {authOptions.Issuer}" : "off"));

    app.Lifetime.ApplicationStopping.Register(() =>
    {
        Log.Information("");
        Log.Information("Application shutting down...");
        Log.CloseAndFlush();
    });

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal("");
    Log.Fatal(ex, "Application failed to start.");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

static void ConfigureMonitorClient(HttpClient client)
{
    client.Timeout = TimeSpan.FromSeconds(10);
    client.MaxResponseContentBufferSize = 8 * 1024 * 1024;
    // some origins reject bare user agents
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Warden-Uptime-Monitor/1.0 (+https://github.com/hawkinslabdev/warden)");
}

static Func<HttpContext, RateLimitPartition<string>> PerIp(int permitPerMinute) => httpContext =>
    RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });

static HashSet<string> ScanIconSlugs(string dir, string format)
{
    var slugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if (!Directory.Exists(dir))
        return slugs;
    foreach (var file in Directory.EnumerateFiles(dir, $"*.{format}"))
        slugs.Add(Path.GetFileNameWithoutExtension(file));
    return slugs;
}

static string NormalizeBasePath(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw)) return "";
    var trimmed = "/" + raw.Trim().Trim('/');
    return trimmed == "/" ? "" : trimmed;
}

static void LogApplicationBanner()
{
    Log.Information("");
    Log.Information("Warden - a markdown-driven uptime status page, built on .NET");
    Log.Information("");
}
