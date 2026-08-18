using Warden.Models;
using Warden.Services;

namespace Warden.Tests;

public sealed class ThemeProviderTests
{
    [Fact]
    public void BuildCustomCssLink_ConfiguredRootRelative_ResolvesBasePath()
    {
        var theme = new ThemeOptions { CustomCssUrl = "/theme/custom.css" };
        var result = ThemeProvider.BuildCustomCssLink(theme, basePath: "/docs");
        Assert.Contains("href=\"/docs/theme/custom.css\"", result);
    }

    [Fact]
    public void BuildCustomCssLink_ConfiguredAbsoluteUrl_Unchanged()
    {
        var theme = new ThemeOptions { CustomCssUrl = "https://cdn.example.com/styles.css" };
        var result = ThemeProvider.BuildCustomCssLink(theme, basePath: "/docs");
        Assert.Contains("href=\"https://cdn.example.com/styles.css\"", result);
    }

    [Fact]
    public void BuildCustomCssLink_NoConfiguredUrl_FallsBackToAutoDetected()
    {
        var theme = new ThemeOptions();
        var result = ThemeProvider.BuildCustomCssLink(theme, autoDetectedCssUrl: "/base/theme/custom.css", basePath: "/docs");
        Assert.Contains("href=\"/base/theme/custom.css\"", result);
    }

    [Fact]
    public void BuildCustomCssLink_NullThemeAndNullAuto_ReturnsEmpty()
    {
        var result = ThemeProvider.BuildCustomCssLink(null, autoDetectedCssUrl: null, basePath: "/docs");
        Assert.Equal("", result);
    }

    [Fact]
    public void BuildCustomJsScript_ConfiguredRootRelative_ResolvesBasePath()
    {
        var theme = new ThemeOptions { CustomJsUrl = "/theme/custom.js" };
        var result = ThemeProvider.BuildCustomJsScript(theme, basePath: "/docs");
        Assert.Contains("src=\"/docs/theme/custom.js\"", result);
    }

    [Fact]
    public void BuildCustomJsScript_ConfiguredAbsoluteUrl_Unchanged()
    {
        var theme = new ThemeOptions { CustomJsUrl = "https://cdn.example.com/script.js" };
        var result = ThemeProvider.BuildCustomJsScript(theme, basePath: "/docs");
        Assert.Contains("src=\"https://cdn.example.com/script.js\"", result);
    }

    [Fact]
    public void BuildCustomJsScript_NoConfiguredUrl_FallsBackToAutoDetected()
    {
        var theme = new ThemeOptions();
        var result = ThemeProvider.BuildCustomJsScript(theme, autoDetectedJsUrl: "/base/theme/custom.js", basePath: "/docs");
        Assert.Contains("src=\"/base/theme/custom.js\"", result);
    }

    [Fact]
    public void BuildCustomJsScript_NullThemeAndNullAuto_ReturnsEmpty()
    {
        var result = ThemeProvider.BuildCustomJsScript(null, autoDetectedJsUrl: null, basePath: "/docs");
        Assert.Equal("", result);
    }
}
