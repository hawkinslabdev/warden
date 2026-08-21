using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Warden.Configuration;

namespace Warden.Tests;

/// <summary>Boots the real app in-memory against a temp content dir; real routing, middleware, rate limiter, CSP and ETag flow</summary>
public class WardenWebApplicationFactory : WebApplicationFactory<Program>
{
    public string ContentDir { get; } =
        Path.Combine(Path.GetTempPath(), "warden-integration-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(Path.Combine(ContentDir, "posts"));
        Directory.CreateDirectory(Path.Combine(ContentDir, "pages"));
        File.WriteAllText(Path.Combine(ContentDir, "posts", "hello-world.md"),
            "---\ntitle: Hello World\ndate: 2026-01-02\ntags: [guide]\n---\n\n# Hello World\n\nInstallation instructions here.\n");
        File.WriteAllText(Path.Combine(ContentDir, "posts", "second-post.md"),
            "---\ntitle: Second Post\ndate: 2026-01-05\ntags: [guide, meta]\n---\n\nMore words to read.\n");
        File.WriteAllText(Path.Combine(ContentDir, "pages", "about.md"),
            "---\ntitle: About\ndescription: About this blog\n---\n\n# About\n\nColophon here.\n");
        File.WriteAllText(Path.Combine(ContentDir, "config.json"),
            """{"title": "Test Blog"}""");
        Directory.CreateDirectory(Path.Combine(ContentDir, "assets"));
        File.WriteAllText(Path.Combine(ContentDir, "assets", "logo.svg"),
            """<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>""");

        // Port 0 keeps the pre-bind port probe from colliding with a running dev server
        builder.UseSetting("urls", "http://127.0.0.1:0");
        builder.UseSetting("Docs:RootPath", ContentDir);
        builder.UseSetting("Docs:EnableHotReload", "false");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (Directory.Exists(ContentDir))
            Directory.Delete(ContentDir, true);
    }
}

public sealed class IntegrationTests : IClassFixture<WardenWebApplicationFactory>
{
    private readonly WardenWebApplicationFactory _factory;

    public IntegrationTests(WardenWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Root_RendersStatusPageDirectly()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("status-unavailable", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Post_RepeatRequest_KeepsTheETagButSendsAFreshNonce()
    {
        var client = _factory.CreateClient();
        var first = await client.GetAsync("/posts/hello-world");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var etag = first.Headers.ETag;
        Assert.NotNull(etag);

        var request = new HttpRequestMessage(HttpMethod.Get, "/posts/hello-world");
        request.Headers.IfNoneMatch.Add(etag!);
        var second = await client.SendAsync(request);

        // The nonce is drawn per response, so a cached body may never be revalidated into a 304.
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(etag, second.Headers.ETag);
        Assert.NotEqual(
            Nonce(first.Headers.GetValues("Content-Security-Policy").Single()),
            Nonce(second.Headers.GetValues("Content-Security-Policy").Single()));
    }

    private static string Nonce(string csp)
    {
        var start = csp.IndexOf("'nonce-", StringComparison.Ordinal) + "'nonce-".Length;
        return csp[start..csp.IndexOf('\'', start)];
    }

    [Fact]
    public async Task StandalonePage_ServedAtSlug()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/about");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Colophon here.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownPage_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/no/such/page");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApiPages_ReturnsSummaries_WithoutServerFilePaths()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/pages");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Contains("\"path\":\"posts/hello-world\"", json);
        Assert.Contains("\"title\":\"Hello World\"", json);
        // Contract; the response must never leak OriginalRelativePath or any server file path
        Assert.DoesNotContain(".md", json);
        Assert.DoesNotContain("originalRelativePath", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApiStatus_ReturnsMonitorsIncidentsAndMaintenance()
    {
        var client = _factory.CreateClient();

        var json = await client.GetStringAsync("/api/status");

        Assert.Contains("\"monitors\":", json);
        Assert.Contains("\"incidents\":", json);
        Assert.Contains("\"maintenance\":", json);
    }

    [Fact]
    public async Task ApiBuildVersion_IsNotCached()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/build-version");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"version\":", json);
    }

    [Fact]
    public async Task Raw_ValidPost_ReturnsMarkdownAttachment()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/raw/posts/hello-world");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/markdown", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Installation instructions here.", body);
    }

    [Theory]
    [InlineData("/raw/..%2Fappsettings.json")]
    [InlineData("/raw/..%2f..%2fappsettings.json")]
    [InlineData("/raw/%2e%2e/appsettings.json")]
    [InlineData("/raw/nonexistent")]
    public async Task Raw_TraversalOrUnknownPath_Returns404(string url)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(url);

        // 400 is also acceptable; the framework may reject encoded dot-segments before routing
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
            $"Expected 404/400 for {url}, got {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Seo_RobotsAndSitemap_Respond()
    {
        var client = _factory.CreateClient();

        var robots = await client.GetStringAsync("/robots.txt");
        Assert.Contains("Sitemap:", robots);

        var sitemap = await client.GetStringAsync("/sitemap.xml");
        Assert.Contains("<urlset", sitemap);
        Assert.Contains("priority>1.0", sitemap);
    }
}

/// <summary>Own factory instance; the reserved-route pages must not leak into the shared fixture's tag/archive tests</summary>
public sealed class ReservedRouteRedirectTests
{
    private sealed class RedirectFactory : WardenWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            File.WriteAllText(Path.Combine(ContentDir, "pages", "tags.md"),
                "---\ntitle: Tags\nredirect: /\n---\n");
            File.WriteAllText(Path.Combine(ContentDir, "pages", "archive.md"),
                "---\ntitle: Archive\nredirect: /about\n---\n");
            File.WriteAllText(Path.Combine(ContentDir, "pages", "authors.md"),
                "---\ntitle: Authors\nredirect: /about\n---\n");
        }
    }

    [Theory]
    [InlineData("/tags", "/")]
    [InlineData("/archive", "/about/")]
    [InlineData("/authors", "/about/")]
    public async Task ReservedRoute_WithRedirectFrontMatter_Redirects(string route, string expected)
    {
        using var factory = new RedirectFactory();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(expected, response.Headers.Location?.ToString());
    }
}

/// <summary>Own factory instance; burning the rate-limit budget must not starve shared-fixture tests (no remote IP on TestServer, all requests share one partition)</summary>
public sealed class RateLimitIntegrationTests
{
    [Fact]
    public async Task ApiStatus_OverLimit_Returns429()
    {
        using var factory = new WardenWebApplicationFactory();
        var client = factory.CreateClient();

        var lastStatus = HttpStatusCode.OK;
        for (var i = 0; i < 35; i++)
        {
            var response = await client.GetAsync("/api/status");
            lastStatus = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastStatus);
    }
}

public sealed class DatabasePathAliasTests
{
    private sealed class DatabasePathAliasFactory(string databasePath) : WardenWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            // top-level `DatabasePath` is the docker-compose-friendly alias for `Monitoring__DatabasePath`
            builder.UseSetting("DatabasePath", databasePath);
        }
    }

    [Fact]
    public void FlatDatabasePathSetting_OverridesNestedMonitoringDefault()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"warden-alias-{Guid.NewGuid():N}.db");
        try
        {
            using var factory = new DatabasePathAliasFactory(dbPath);
            using var forceStart = factory.CreateClient();
            Assert.Equal(dbPath, factory.Services.GetRequiredService<MonitoringOptions>().DatabasePath);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var path in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
                if (File.Exists(path)) File.Delete(path);
        }
    }
}
