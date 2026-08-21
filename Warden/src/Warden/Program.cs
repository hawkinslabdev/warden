using System.IO.Compression;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Serilog;
using System.Threading.RateLimiting;
using Warden.Configuration;
using Warden.Endpoints;
using Warden.Models;
using Warden.Serialization;
using Warden.Services;
using Warden.Services.MarkdownExtensions;

Directory.CreateDirectory("log");

var cliArgs = CliArguments.Parse(args);
var exportDir = cliArgs.ExportDir;
var exportBaseUrl = cliArgs.ExportBaseUrl;

try
{
    var builder = WebApplication.CreateBuilder(args);

    Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .CreateLogger();

    builder.Host.UseSerilog();

    var docsOptions = builder.Configuration.GetSection("Docs").Get<DocsOptions>() ?? new DocsOptions();
    if (exportDir != null)
        docsOptions = docsOptions with { EnableHotReload = false }; // no file watcher needed for a one-shot export

    var basePath = NormalizeBasePath(cliArgs.BasePath ?? docsOptions.BasePath);
    docsOptions = docsOptions with { BasePath = basePath };

    builder.Services.AddSingleton(docsOptions);

    var monitoringOptions = builder.Configuration.GetSection("Monitoring").Get<MonitoringOptions>() ?? new MonitoringOptions();
    // plain `DatabasePath` is a docker-compose-friendly alias for `Monitoring__DatabasePath`; wins over it when both are set
    if (builder.Configuration["DatabasePath"] is { Length: > 0 } databasePathAlias)
        monitoringOptions = monitoringOptions with { DatabasePath = databasePathAlias };
    builder.Services.AddSingleton(monitoringOptions);
    builder.Services.AddSingleton<HeartbeatStore>();
    void ConfigureMonitorClient(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(10);
        client.MaxResponseContentBufferSize = 8 * 1024 * 1024;
        // some origins (Forgejo instances, api.github.com, ...) 403 a bare User-Agent regardless of reachability - identify as a real client
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Warden-Uptime-Monitor/1.0 (+https://github.com/melosso/warden)");
    }
    builder.Services.AddHttpClient(MonitorScheduler.HttpClientName, ConfigureMonitorClient);
    // a target opts into this with "insecure": true for a self-signed/untrusted internal service; every other target keeps strict validation
    builder.Services.AddHttpClient(MonitorScheduler.InsecureHttpClientName, ConfigureMonitorClient)
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

    var proxyOptions = builder.Configuration.GetSection("Proxy").Get<ProxyOptions>() ?? new ProxyOptions();
    builder.Services.Configure<ForwardedHeadersOptions>(options => ForwardedHeaderSetup.Configure(options, proxyOptions));

    var docsRootAbsolute = Path.GetFullPath(docsOptions.RootPath).Replace(Path.DirectorySeparatorChar, '/');

    // appsettings.json's Docs:Themes wins if present; theme.json is the file-only alternative.
    var themeOptions = builder.Configuration.GetSection("Docs:Themes").Get<ThemeOptions>()
        ?? ThemeJsonLoader.Load(builder.Environment.WebRootPath)
        ?? new ThemeOptions();
    builder.Services.AddSingleton(themeOptions);

    var codeGroupIconOptions = builder.Configuration.GetSection("Docs:CodeGroupIcons").Get<CodeGroupIconOptions>()
        ?? new CodeGroupIconOptions();
    // Only render tab icons for existing slugs to avoid 404s
    if (codeGroupIconOptions.Enabled && codeGroupIconOptions.BaseUrl.StartsWith('/') && !codeGroupIconOptions.BaseUrl.StartsWith("//"))
    {
        var iconWebRoot = builder.Environment.WebRootPath
            ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
        var available = ScanIconSlugs(
            Path.Combine(iconWebRoot, codeGroupIconOptions.BaseUrl.Trim('/')),
            codeGroupIconOptions.Format);
        if (available.Count > 0)
            codeGroupIconOptions = codeGroupIconOptions with { Available = available };
    }
    builder.Services.AddSingleton(codeGroupIconOptions);

    builder.Services.AddSingleton<ISyntaxHighlighter, TextMateSyntaxHighlighter>();
    builder.Services.AddSingleton<MathRenderer>();
    builder.Services.AddSingleton(sp => new MarkdownService(
        sp.GetRequiredService<ISyntaxHighlighter>(), basePath,
        sp.GetRequiredService<CodeGroupIconOptions>(),
        sp.GetRequiredService<MathRenderer>(),
        sp.GetRequiredService<ILogger<MarkdownService>>()));
    builder.Services.AddSingleton<ContentService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<ContentService>());
    // after ContentService: hosted services start in registration order, and the first tick needs SiteConfig built
    builder.Services.AddHostedService<MonitorScheduler>();

    // plain GIT_URL/GIT_USERNAME/GIT_PASSWORD/GIT_CRON/GIT_ENABLED aliases, so a docker-compose .env file
    // doesn't need the Git__ double-underscore convention; Git__* still wins when both are set
    var gitSyncOptions = builder.Configuration.GetSection("Git").Get<GitSyncOptions>() ?? new GitSyncOptions();
    gitSyncOptions = gitSyncOptions with
    {
        Enabled = gitSyncOptions.Enabled || Environment.GetEnvironmentVariable("GIT_ENABLED") is "true" or "1",
        Url = gitSyncOptions.Url ?? Environment.GetEnvironmentVariable("GIT_URL"),
        Username = gitSyncOptions.Username ?? Environment.GetEnvironmentVariable("GIT_USERNAME"),
        Password = gitSyncOptions.Password ?? Environment.GetEnvironmentVariable("GIT_PASSWORD"),
        Cron = Environment.GetEnvironmentVariable("Git__Cron") ?? Environment.GetEnvironmentVariable("GIT_CRON") ?? gitSyncOptions.Cron,
    };
    if (exportDir is null) // no background pulls during a one-shot static export
        builder.Services.AddHostedService(sp => new GitContentSyncService(
            gitSyncOptions, docsRootAbsolute, sp.GetRequiredService<ILogger<GitContentSyncService>>()));

    var customCspRaw = builder.Configuration["Docs:ContentSecurityPolicy"];
    var customCsp = string.IsNullOrWhiteSpace(customCspRaw) ? null : customCspRaw;

    // WebRootPath is null when wwwroot/ is missing (e.g. under test hosts); fall back to the conventional path
    var webRootPath = builder.Environment.WebRootPath
        ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot");

    // Drop files at wwwroot/theme/custom.{css,js} and they're picked up at startup, no config edit needed. Does NOT support hot reloading!
    var themeDir = Path.Combine(webRootPath, "theme");
    try { Directory.CreateDirectory(themeDir); } catch (IOException) { }
    var autoCustomCssUrl = File.Exists(Path.Combine(themeDir, "custom.css")) ? $"{basePath}/theme/custom.css" : null;
    var autoCustomJsUrl = File.Exists(Path.Combine(themeDir, "custom.js")) ? $"{basePath}/theme/custom.js" : null;

    builder.Services.AddSingleton(new PageRequestSettings(
        BasePath: basePath,
        CustomCsp: customCsp,
        AutoCustomCssUrl: autoCustomCssUrl,
        AutoCustomJsUrl: autoCustomJsUrl,
        WebRootPath: webRootPath,
        DocsRootAbsolute: docsRootAbsolute,
        PublicBaseUrl: PageRequestSettings.ResolvePublicBaseUrl(
            exportBaseUrl, docsOptions.PublicBaseUrl, builder.Configuration["PublicBaseUrl"]),
        CliTheme: cliArgs.Theme,
        CliStructure: cliArgs.Structure));
    builder.Services.AddSingleton<PageResponder>();
    builder.Services.AddSingleton<PageRequestHandler>();

    builder.Services.ConfigureHttpJsonOptions(opts =>
        opts.SerializerOptions.TypeInfoResolverChain.Insert(0, WardenJsonContext.Default));

    builder.Services.AddResponseCompression(opts =>
    {
        opts.EnableForHttps = true;
        opts.Providers.Add<BrotliCompressionProvider>();
        opts.Providers.Add<GzipCompressionProvider>();
    });

    builder.Services.Configure<BrotliCompressionProviderOptions>(opts =>
    {
        opts.Level = CompressionLevel.Fastest;
    });

    builder.Services.Configure<GzipCompressionProviderOptions>(opts =>
    {
        opts.Level = CompressionLevel.Fastest;
    });

    builder.WebHost.ConfigureKestrel(KestrelHardening.Configure);

    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(opts => opts.FormatterName = "simple");
    builder.Logging.AddSimpleConsole(opts => opts.TimestampFormat = "[yyyy-MM-dd HH:mm:ss] ");

    builder.Services.AddSingleton<Serilog.ILogger>(sp => Log.Logger);

    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy(RateLimitPolicies.Api, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    LogApplicationBanner();

    var app = builder.Build();

    if (!app.Environment.IsDevelopment() && app.Services.GetRequiredService<PageRequestSettings>().PublicBaseUrl is null)
        Log.Warning("Docs:PublicBaseUrl is not set; canonical URLs, feeds and robots.txt are built from the caller's Host header. Set it in production.");

    // Must finish before ContentService's async renders the pages
    await app.Services.GetRequiredService<ISyntaxHighlighter>().InitializeAsync(CancellationToken.None);

    app.UseForwardedHeaders();

    if (basePath.Length > 0)
        app.UsePathBase(basePath);

    app.UseSecurityHeaders(customCsp);
    app.UseResponseCompression();

    var defaultWebRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot-default");
    if (Directory.Exists(defaultWebRoot) && Directory.Exists(webRootPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new CompositeFileProvider(
                new PhysicalFileProvider(webRootPath),
                new PhysicalFileProvider(defaultWebRoot)
            )
        });
    }
    else
    {
        app.UseStaticFiles();
    }

    // content/assets/ at /assets/, restricted to a media allowlist so scripts, html and archives 404.
    var assetsDir = Path.Combine(Path.GetFullPath(docsOptions.RootPath), "assets");
    AssetVersioning.Current = new AssetVersioning(assetsDir);
    if (Directory.Exists(assetsDir))
    {
        var assetContentTypes = AssetContentTypes.Provider();
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(assetsDir),
            RequestPath = "/assets",
            ContentTypeProvider = assetContentTypes,
            ServeUnknownFileTypes = false,
            OnPrepareResponse = ctx =>
            {
                ctx.Context.Response.Headers.CacheControl =
                    !app.Environment.IsDevelopment() && ctx.Context.Request.Query.ContainsKey("v")
                        ? "public,max-age=31536000,immutable"
                        : "no-cache";

                // An .svg navigated to directly runs its own inline script in this origin; the page nonce never reaches static responses.
                if (ctx.File.Name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                    ctx.Context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; sandbox";
            }
        });
    }

    app.UseRouting();
    app.UseRateLimiter();

    app.MapHealthEndpoints();
    app.MapApiEndpoints();
    app.MapSeoEndpoints();
    app.MapStatusEndpoints();
    app.MapContentEndpoints();

    if (exportDir != null)
    {
        await StaticSiteExporter.RunAsync(app, exportDir, exportBaseUrl, CancellationToken.None);
        Log.Information("Static export written to {Dir}", exportDir);
        return;
    }

    var urls = app.Urls.Count > 0
        ? app.Urls.ToArray()
        : (Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
            ?? builder.Configuration["urls"]
            ?? "http://localhost:5000").Split(';');

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

void LogApplicationBanner()
{
    Log.Information("");
    Log.Information("Warden - a markdown-driven uptime status page, built on .NET");
    Log.Information("");
}
