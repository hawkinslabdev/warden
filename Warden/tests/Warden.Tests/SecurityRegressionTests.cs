using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Warden.Configuration;
using Warden.Services;
using Warden.Services.MarkdownExtensions;

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
        // Accepted risk: downstream sites author raw HTML, so the pipeline does not DisableHtml().
        // Script execution is blocked by the nonce CSP when served and the meta CSP when exported;
        // what stays open is non-script markup injection from whoever can commit content.
        // Flipping this assertion means reopening that decision, not fixing a bug.
        var markdown = new MarkdownService();
        var result = markdown.Parse("<script>alert('xss')</script>\n\n# Heading");

        Assert.Contains("<script>alert('xss')</script>", result.Html);
    }

    [Fact]
    public void MarkdownService_GenericAttributes_DropHandlersAndKeepPresentation()
    {
        // DisableHtml does not cover `{...}` attributes, which reach any element.
        var markdown = new MarkdownService();

        var handler = markdown.Parse("Some text{onclick=\"alert(1)\"}");
        Assert.DoesNotContain("onclick", handler.Html);

        var hrefOverride = markdown.Parse("[x](/safe){href=\"javascript:alert(1)\"}");
        Assert.DoesNotContain("javascript:", hrefOverride.Html);

        var allowed = markdown.Parse("[x](https://example.com){target=\"_blank\" rel=\"noopener\" .lead}");
        Assert.Contains("target=\"_blank\"", allowed.Html);
        Assert.Contains("lead", allowed.Html);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(64)]
    public void MathRenderer_AcceptsRealisticNesting(int depth) =>
        Assert.Null(MathRenderer.Reject(Nested(depth)));

    [Theory]
    [InlineData(65)]
    [InlineData(5000)]
    public void MathRenderer_RejectsNestingThatWouldOverflowTheStack(int depth) =>
        Assert.NotNull(MathRenderer.Reject(Nested(depth)));

    [Fact]
    public void MathRenderer_DeepNestingRendersAnErrorInsteadOfKillingTheProcess()
    {
        // A stack overflow is uncatchable: before the cap this aborted the process and re-fired on every restart.
        var html = new MathRenderer().RenderToHtml(Nested(5000), displayMode: true);

        Assert.Contains("math-error", html);
        Assert.Contains("katex", new MathRenderer().RenderToHtml("E = mc^2", displayMode: false));
    }

    private static string Nested(int depth) =>
        string.Concat(Enumerable.Repeat("\\frac{", depth)) + "x" + string.Concat(Enumerable.Repeat("}{y}", depth));

    [Fact]
    public void AssetContentTypes_ExcludeExecutableAndMarkupExtensions()
    {
        Assert.False(AssetContentTypes.IsAllowed("/content/assets/probe.html"));
        Assert.False(AssetContentTypes.IsAllowed("/content/assets/payload.js"));
        Assert.False(AssetContentTypes.IsAllowed("/content/assets/bundle.zip"));
        Assert.True(AssetContentTypes.IsAllowed("/content/assets/cover.webp"));
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
