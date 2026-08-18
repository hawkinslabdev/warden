using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Warden.Configuration;
using Warden.Services;

namespace Warden.Tests;

public sealed class SecurityRegressionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ContentService _service;

    public SecurityRegressionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "warden-security-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);

        var options = new DocsOptions
        {
            RootPath = _tempDir,
            DefaultPage = "index",
            EnableHotReload = false
        };
        _service = new ContentService(options, new MarkdownService(), NullLogger<ContentService>.Instance);
    }

    public void Dispose()
    {
        _service.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private async Task CreateIndexPage()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "index.md"), "# Home\n");
        await _service.StartAsync(CancellationToken.None);
    }

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..%2f..%2fetc%2fpasswd")]
    [InlineData("getting-started/../../../etc/passwd")]
    public async Task GetPageAsync_PathTraversalAttempt_ReturnsNull(string maliciousPath)
    {
        await CreateIndexPage();

        var page = await _service.GetPageAsync(maliciousPath);

        Assert.Null(page);
    }

    [Theory]
    [InlineData("index\0.md")]
    [InlineData("getting-started\0/../../etc/passwd")]
    public async Task GetPageAsync_NullByteInPath_HandledSafely(string maliciousPath)
    {
        await CreateIndexPage();

        var page = await _service.GetPageAsync(maliciousPath);

        Assert.Null(page);
    }

    [Fact]
    public void MarkdownService_RawHtmlInContent_PassesThroughUnsanitized()
    {
        // Markdig does not sanitize raw HTML by design; docs are authored content, but this must not change silently.
        var markdown = new MarkdownService();
        var result = markdown.Parse("<script>alert('xss')</script>\n\n# Heading");

        Assert.Contains("<script>alert('xss')</script>", result.Html);
    }

    [Fact]
    public async Task SecurityHeaders_Apply_SetsExpectedHeaders()
    {
        var context = new DefaultHttpContext();

        await SecurityHeaders.Apply(context, () => Task.CompletedTask);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"]);
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"]);
        Assert.Contains("default-src 'self'", context.Response.Headers["Content-Security-Policy"].ToString());
    }

    [Fact]
    public async Task SecurityHeaders_ImgSrc_StaysSelfOnly_CodeGroupIconsAreVendoredLocally()
    {
        // Code-group icons ship under wwwroot/icons, so a widened img-src means BaseUrl was pointed at a CDN again.
        var context = new DefaultHttpContext();
        await SecurityHeaders.Apply(context, () => Task.CompletedTask);

        var csp = context.Response.Headers["Content-Security-Policy"].ToString();
        var imgSrc = csp.Split(';').Single(d => d.Trim().StartsWith("img-src"));

        Assert.Contains("'self'", imgSrc);
        Assert.DoesNotContain("jsdelivr", imgSrc);
    }
}
