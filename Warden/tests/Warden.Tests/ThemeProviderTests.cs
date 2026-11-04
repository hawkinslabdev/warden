using Warden.Models;
using Warden.Services;

namespace Warden.Tests;

public sealed class ThemeProviderTests : IDisposable
{
    private readonly string _themeDir = Path.Combine(Path.GetTempPath(), $"warden-theme-test-{Guid.NewGuid():N}");

    public ThemeProviderTests() => Directory.CreateDirectory(_themeDir);
    public void Dispose() => Directory.Delete(_themeDir, recursive: true);

    [Fact]
    public void BuildThemeCss_PrimaryColor_SetsAccentInLightBlock()
    {
        var css = ThemeProvider.BuildThemeCss(new ThemeOptions { PrimaryColor = "#3E63DD" });
        Assert.Contains(":root {\n    --primary-color: #3E63DD;\n    --accent: #3E63DD;", css);
    }

    [Fact]
    public void BuildThemeCss_DarkColors_EmitBothDarkSelectors()
    {
        var css = ThemeProvider.BuildThemeCss(new ThemeOptions { PrimaryColorDark = "#9DB1FF", AccentLightDark = "#252C4A" });
        const string dark = "{\n    --primary-color: #9DB1FF;\n    --accent: #9DB1FF;\n    --accent-light: #252C4A;\n}";
        Assert.Contains($"@media (prefers-color-scheme: dark) {{\n:root:not([data-theme=\"light\"]) {dark}", css);
        Assert.Contains($":root[data-theme=\"dark\"] {dark}", css);
    }

    [Fact]
    public void BuildThemeCss_LightMode_OmitsDarkColors()
    {
        var css = ThemeProvider.BuildThemeCss(new ThemeOptions { Mode = "light", PrimaryColorDark = "#9DB1FF" });
        Assert.Empty(css);
    }

    [Fact]
    public void BuildCustomCssLink_ConfiguredRootRelative_ResolvesBasePath()
    {
        var theme = new ThemeOptions { CustomCssUrl = "/theme/custom.css" };
        var result = ThemeProvider.BuildCustomCssLink(theme, _themeDir, basePath: "/docs");
        Assert.Contains("href=\"/docs/theme/custom.css\"", result);
    }

    [Fact]
    public void BuildCustomCssLink_ConfiguredAbsoluteUrl_Unchanged()
    {
        var theme = new ThemeOptions { CustomCssUrl = "https://cdn.example.com/styles.css" };
        var result = ThemeProvider.BuildCustomCssLink(theme, _themeDir, basePath: "/docs");
        Assert.Contains("href=\"https://cdn.example.com/styles.css\"", result);
    }

    [Fact]
    public void BuildCustomCssLink_NoConfiguredUrl_FallsBackToAutoDetected()
    {
        File.WriteAllText(Path.Combine(_themeDir, "custom.css"), "");
        var result = ThemeProvider.BuildCustomCssLink(new ThemeOptions(), _themeDir, basePath: "/docs");
        Assert.Contains("href=\"/docs/theme/custom.css\"", result);
    }

    [Fact]
    public void BuildCustomCssLink_NullThemeAndNoFile_ReturnsEmpty()
    {
        var result = ThemeProvider.BuildCustomCssLink(null, _themeDir, basePath: "/docs");
        Assert.Equal("", result);
    }

    [Fact]
    public void BuildCustomJsScript_ConfiguredRootRelative_ResolvesBasePath()
    {
        var theme = new ThemeOptions { CustomJsUrl = "/theme/custom.js" };
        var result = ThemeProvider.BuildCustomJsScript(theme, _themeDir, basePath: "/docs");
        Assert.Contains("src=\"/docs/theme/custom.js\"", result);
    }

    [Fact]
    public void BuildCustomJsScript_ConfiguredAbsoluteUrl_Unchanged()
    {
        var theme = new ThemeOptions { CustomJsUrl = "https://cdn.example.com/script.js" };
        var result = ThemeProvider.BuildCustomJsScript(theme, _themeDir, basePath: "/docs");
        Assert.Contains("src=\"https://cdn.example.com/script.js\"", result);
    }

    [Fact]
    public void BuildCustomJsScript_NoConfiguredUrl_FallsBackToAutoDetected()
    {
        File.WriteAllText(Path.Combine(_themeDir, "custom.js"), "");
        var result = ThemeProvider.BuildCustomJsScript(new ThemeOptions(), _themeDir, basePath: "/docs");
        Assert.Contains("src=\"/docs/theme/custom.js\"", result);
    }

    [Fact]
    public void BuildCustomJsScript_NullThemeAndNoFile_ReturnsEmpty()
    {
        var result = ThemeProvider.BuildCustomJsScript(null, _themeDir, basePath: "/docs");
        Assert.Equal("", result);
    }
}
