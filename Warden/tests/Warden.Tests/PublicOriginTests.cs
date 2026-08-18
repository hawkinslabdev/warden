using Microsoft.AspNetCore.Http;
using Warden.Services;

namespace Warden.Tests;

public sealed class PublicOriginTests
{
    private static PageRequestSettings Settings(string? publicBaseUrl) =>
        new("", null, null, null, "wwwroot", "/content", publicBaseUrl);

    private static HttpContext Request(string host, string scheme = "https")
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        return context;
    }

    [Fact]
    public void Origin_PrefersConfiguredPublicBaseUrl_OverHostHeader()
    {
        var origin = Settings("https://blog.example.com").Origin(Request("evil.example.com"));

        Assert.Equal("https://blog.example.com", origin);
    }

    [Fact]
    public void Origin_TrimsTrailingSlashFromConfiguredValue()
    {
        var origin = Settings("https://blog.example.com/").Origin(Request("evil.example.com"));

        Assert.Equal("https://blog.example.com", origin);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Origin_FallsBackToRequestHost_WhenUnconfigured(string? configured)
    {
        var origin = Settings(configured).Origin(Request("localhost:5000", scheme: "http"));

        Assert.Equal("http://localhost:5000", origin);
    }

    [Fact]
    public void Origin_TreatsWhitespaceOnlyAsUnset()
    {
        var origin = Settings("   ").Origin(Request("localhost:5000", scheme: "http"));

        Assert.Equal("http://localhost:5000", origin);
    }

    [Fact]
    public void ResolvePublicBaseUrl_EmptyDocsOptionDoesNotMaskTheAlias() =>
        Assert.Equal("https://blog.example.com",
            PageRequestSettings.ResolvePublicBaseUrl(null, "", "https://blog.example.com"));

    [Fact]
    public void ResolvePublicBaseUrl_CliWinsOverBothConfigSources() =>
        Assert.Equal("https://cli.example.com",
            PageRequestSettings.ResolvePublicBaseUrl("https://cli.example.com", "https://docs.example.com", "https://alias.example.com"));

    [Fact]
    public void ResolvePublicBaseUrl_BlankEverywhereResolvesToNull() =>
        Assert.Null(PageRequestSettings.ResolvePublicBaseUrl("", "  ", ""));

    [Fact]
    public void Normalize_StripsTrailingSlashAndSurroundingWhitespace() =>
        Assert.Equal("https://blog.example.com", PageRequestSettings.Normalize("  https://blog.example.com/  "));
}
