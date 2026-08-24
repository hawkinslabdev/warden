using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Warden.Models;
using Warden.Services.Layout;
using Warden.Services.Theming;

namespace Warden.Tests;

public sealed class ThemeRegistryTests
{
    [Fact]
    public void Resolve_NullOrBlank_ReturnsDefault()
    {
        Assert.Same(ThemeRegistry.Default, ThemeRegistry.Resolve(null));
        Assert.Same(ThemeRegistry.Default, ThemeRegistry.Resolve("   "));
    }

    [Fact]
    public void Resolve_UnknownName_FallsBackToDefaultWithoutThrowing()
    {
        Assert.Same(ThemeRegistry.Default, ThemeRegistry.Resolve("no-such-theme"));
    }

    [Theory]
    [InlineData("deep-space")]
    [InlineData("DEEP-SPACE")]
    [InlineData("  Deep-Space  ")]
    public void Resolve_IsCaseAndWhitespaceInsensitive(string name)
    {
        Assert.Equal("deep-space", ThemeRegistry.Resolve(name).Name);
    }

    [Fact]
    public void All_NamesAreUniqueAndKebabCase()
    {
        var names = ThemeRegistry.All.Select(t => t.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(names, n => Assert.Matches("^[a-z][a-z0-9-]*$", n));
    }
}

public sealed class StructureRegistryTests
{
    [Fact]
    public void Resolve_NullOrBlank_ReturnsDefault()
    {
        Assert.Same(StructureRegistry.Default, StructureRegistry.Resolve(null));
        Assert.Same(StructureRegistry.Default, StructureRegistry.Resolve("   "));
    }

    [Fact]
    public void Resolve_UnknownName_FallsBackToDefaultWithoutThrowing()
    {
        Assert.Same(StructureRegistry.Default, StructureRegistry.Resolve("no-such-structure"));
    }

    [Theory]
    [InlineData("clean")]
    [InlineData("CLEAN")]
    [InlineData("  Clean  ")]
    public void Resolve_IsCaseAndWhitespaceInsensitive(string name)
    {
        Assert.Equal("clean", StructureRegistry.Resolve(name).Name);
    }

    [Fact]
    public void CleanIsFirstAndDefault() =>
        Assert.Equal("clean", StructureRegistry.Default.Name);

    [Fact]
    public void OnlyDashboardUsesTheGroupedStatusLayout()
    {
        Assert.False(StructureRegistry.Resolve("clean").UseCardStatusLayout);
        Assert.False(StructureRegistry.Resolve("default").UseCardStatusLayout);
        Assert.True(StructureRegistry.Resolve("dashboard").UseCardStatusLayout);
    }

    [Fact]
    public void OnlyCleanHidesTheStatusHeader()
    {
        Assert.False(StructureRegistry.Resolve("clean").ShowStatusHeader);
        Assert.True(StructureRegistry.Resolve("default").ShowStatusHeader);
        Assert.True(StructureRegistry.Resolve("dashboard").ShowStatusHeader);
    }
}

public sealed class ThemeSelectionTests
{
    [Theory]
    [InlineData("dark", "default", ThemeMode.Dark)]
    [InlineData("light", "default", ThemeMode.Light)]
    [InlineData("ocean dark", "ocean", ThemeMode.Dark)]
    [InlineData("dark ocean", "ocean", ThemeMode.Dark)]
    [InlineData("  Ocean   LIGHT ", "ocean", ThemeMode.Light)]
    [InlineData("ocean", "ocean", ThemeMode.Auto)]
    public void ConfigValue_CarriesBothPaletteAndMode(string value, string expectedName, ThemeMode expectedMode)
    {
        var selection = ThemeSelection.Resolve(new ThemeOptions(), cliTheme: null, configTheme: value);

        Assert.Equal(expectedName, selection.Theme.Name);
        Assert.Equal(expectedMode, selection.Mode);
    }

    [Fact]
    public void UnknownName_StillFallsBackToDefault()
    {
        var selection = ThemeSelection.Resolve(new ThemeOptions(), null, "no-such-theme dark");

        Assert.Same(ThemeRegistry.Default, selection.Theme);
        Assert.Equal(ThemeMode.Dark, selection.Mode);
    }

    [Fact]
    public void PaletteAndModeResolveIndependentlyAcrossSources()
    {
        var selection = ThemeSelection.Resolve(new ThemeOptions { Name = "solarized" }, null, "dark");

        Assert.Equal("solarized", selection.Theme.Name);
        Assert.Equal(ThemeMode.Dark, selection.Mode);
    }

    [Fact]
    public void ThemesModeSettingStillAppliesWhenNoValueCarriesOne()
    {
        var selection = ThemeSelection.Resolve(new ThemeOptions { Mode = "dark" }, null, "ocean");

        Assert.Equal("ocean", selection.Theme.Name);
        Assert.Equal(ThemeMode.Dark, selection.Mode);
    }

    [Fact]
    public void CliThemeOutranksLowerSources()
    {
        var selection = ThemeSelection.Resolve(new ThemeOptions { Name = "solarized" }, "laserwave light", "ocean dark");

        Assert.Equal("laserwave", selection.Theme.Name);
        Assert.Equal(ThemeMode.Light, selection.Mode);
    }

    [Fact]
    public void Default_IsTheDefaultTheme() => Assert.Equal("default", ThemeRegistry.Default.Name);

    /// <summary>The default theme ships the current design; adding a theme must never restyle a site that configured none.</summary>
    [Fact]
    public void DefaultTheme_AddsNoComponentCss() => Assert.Equal(string.Empty, ThemeRegistry.Default.ComponentCss);
}

public sealed class ThemeTokenTests
{
    public static TheoryData<string> ThemeNames() => [.. ThemeRegistry.All.Select(t => t.Name)];

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void RequiredPaletteKeys_PresentInBothModes(string name)
    {
        var theme = ThemeRegistry.Resolve(name);
        foreach (var key in IWardenTheme.RequiredPaletteKeys)
        {
            Assert.True(theme.LightTokens.ContainsKey(key), $"{name} light is missing {key}");
            Assert.True(theme.DarkTokens.ContainsKey(key), $"{name} dark is missing {key}");
        }
    }

    /// <summary>A literal declared in one mode bleeds into the other; alias values re-resolve, so only literals need parity.</summary>
    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void EveryLiteralColourHasBothModes(string name)
    {
        var theme = ThemeRegistry.Resolve(name);
        var lightLiterals = theme.LightTokens.Where(t => IsLiteral(t.Value)).Select(t => t.Key);
        var darkLiterals = theme.DarkTokens.Where(t => IsLiteral(t.Value)).Select(t => t.Key);

        Assert.Empty(lightLiterals.Except(theme.DarkTokens.Keys));
        Assert.Empty(darkLiterals.Except(theme.LightTokens.Keys));
    }

    private static bool IsLiteral(string value) =>
        value.StartsWith('#') || value.StartsWith("rgba(", StringComparison.Ordinal);
}

public sealed class ThemeContrastTests
{
    public static TheoryData<string> ThemeNames() => [.. ThemeRegistry.All.Select(t => t.Name)];

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void LightMode_MeetsContrastFloor(string name) =>
        AssertContrastFloor(ThemeRegistry.Resolve(name), dark: false);

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void DarkMode_MeetsContrastFloor(string name) =>
        AssertContrastFloor(ThemeRegistry.Resolve(name), dark: true);

    private static void AssertContrastFloor(IWardenTheme theme, bool dark)
    {
        var tokens = dark
            ? Merge(theme.LightTokens, theme.DarkTokens)
            : theme.LightTokens;

        var bg = tokens["--bg-color"];
        var mode = dark ? "dark" : "light";

        AssertRatio(tokens["--text-color"], bg, 4.5, $"{theme.Name}/{mode} body text");
        AssertRatio(tokens["--text-muted"], bg, 4.5, $"{theme.Name}/{mode} muted text");
        AssertRatio(tokens["--accent"], bg, 4.5, $"{theme.Name}/{mode} accent");
        AssertRatio(tokens["--border"], bg, 1.2, $"{theme.Name}/{mode} hairline border");
        AssertRatio(tokens["--text-color"], tokens["--sidebar-bg"], 4.5, $"{theme.Name}/{mode} text on sidebar");
        AssertRatio(tokens["--text-color"], tokens["--code-bg"], 4.5, $"{theme.Name}/{mode} text on code");
        AssertRatio(tokens["--text-color"], tokens["--accent-light"], 4.5, $"{theme.Name}/{mode} text on accent tint");
    }

    private static Dictionary<string, string> Merge(
        IReadOnlyDictionary<string, string> baseTokens,
        IReadOnlyDictionary<string, string> overrides)
    {
        var merged = new Dictionary<string, string>(baseTokens, StringComparer.Ordinal);
        foreach (var (key, value) in overrides)
            merged[key] = value;
        return merged;
    }

    /// <summary>Only hex pairs are measurable; a translucent token has no fixed ratio to assert.</summary>
    private static void AssertRatio(string foreground, string background, double floor, string label)
    {
        if (!foreground.StartsWith('#') || !background.StartsWith('#'))
            return;

        var ratio = ContrastRatio(foreground, background);
        Assert.True(ratio >= floor, $"{label}: {foreground} on {background} is {ratio:F2}:1, needs {floor}:1");
    }

    internal static double ContrastRatio(string a, string b)
    {
        var (high, low) = (RelativeLuminance(a), RelativeLuminance(b));
        if (low > high)
            (high, low) = (low, high);
        return (high + 0.05) / (low + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        var value = hex.TrimStart('#');
        if (value.Length == 3)
            value = string.Concat(value.Select(c => new string(c, 2)));

        var r = Channel(value[..2]);
        var g = Channel(value.Substring(2, 2));
        var b = Channel(value.Substring(4, 2));
        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);

        static double Channel(string pair)
        {
            var srgb = int.Parse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return srgb <= 0.03928 ? srgb / 12.92 : Math.Pow((srgb + 0.055) / 1.055, 2.4);
        }
    }
}

public sealed partial class ThemeCssIntegrityTests
{
    /// <summary>Set inline on the elements that read them, not by any theme.</summary>
    private static readonly string[] ExternallyDefined =
        ["--shiki-light", "--shiki-dark", "--slug-hue", "--tip-shift", "--icon", "--bar-h"];

    public static TheoryData<string> ThemeNames() => [.. ThemeRegistry.All.Select(t => t.Name)];

    /// <summary>A theme dropping a variable the base stylesheet reads renders as an unstyled element, not an error.</summary>
    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void EveryReferencedVariableIsDefined(string name)
    {
        var css = RenderStyleBlock(ThemeRegistry.Resolve(name));

        var defined = DefinitionPattern().Matches(css).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var referenced = ReferencePattern().Matches(css).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        referenced.ExceptWith(ExternallyDefined);
        referenced.ExceptWith(defined);

        Assert.Empty(referenced);
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void ThemeCssIsLinkedAsASingleExternalStylesheet(string name)
    {
        var html = Render(ThemeRegistry.Resolve(name));
        Assert.Equal(1, StylesheetLinkPattern().Count(html));
        Assert.Contains("<link rel=\"stylesheet\" href=\"/warden.css?v=", html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void ThemeClassIsOnTheHtmlElement(string name)
    {
        var html = Render(ThemeRegistry.Resolve(name));
        Assert.Contains($"class=\"theme-{name}\"", html, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void ComponentCssIsAppendedAfterTheBaseStylesheet(string name)
    {
        var theme = ThemeRegistry.Resolve(name);
        if (theme.ComponentCss.Length == 0)
            return;

        var firstSelector = ComponentCssFirstSelectorPattern().Match(theme.ComponentCss);
        Assert.True(firstSelector.Success, $"{name} component CSS has no recognizable selector to anchor on");

        var css = RenderStyleBlock(theme);
        var at = css.LastIndexOf(firstSelector.Groups[1].Value, StringComparison.Ordinal);
        Assert.True(at >= 0, $"{name} component CSS is missing from the style block");
        Assert.True(at > css.IndexOf(".post-card {", StringComparison.Ordinal),
            $"{name} component CSS must come after the base rules it overrides");
    }

    [Fact]
    public void LightMode_EmitsNoDarkBlock()
    {
        var css = ThemeCssBuilder.BuildTokenCss(ThemeRegistry.Default, ThemeMode.Light);
        Assert.DoesNotContain("prefers-color-scheme", css, StringComparison.Ordinal);
        Assert.DoesNotContain("data-theme", css, StringComparison.Ordinal);
    }

    [Fact]
    public void AutoMode_EmitsBothOsQueryAndExplicitToggle()
    {
        var css = ThemeCssBuilder.BuildTokenCss(ThemeRegistry.Default, ThemeMode.Auto);
        Assert.Contains("@media (prefers-color-scheme: dark)", css, StringComparison.Ordinal);
        Assert.Contains(":root[data-theme=\"dark\"]", css, StringComparison.Ordinal);
    }

    /// <summary>Forced dark has no toggle, so following the OS would let a light OS win.</summary>
    [Fact]
    public void DarkMode_EmitsOnlyTheForcedBlock()
    {
        var css = ThemeCssBuilder.BuildTokenCss(ThemeRegistry.Default, ThemeMode.Dark);
        Assert.DoesNotContain("prefers-color-scheme", css, StringComparison.Ordinal);
        Assert.Contains(":root[data-theme=\"dark\"]", css, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void NotFoundPage_UsesTheActiveThemePalette(string name)
    {
        var theme = ThemeRegistry.Resolve(name);
        var html = LayoutProvider.Get404Layout(LayoutProvider.HtmlEncode, theme: theme);

        Assert.Contains($"class=\"theme-{name}\"", html, StringComparison.Ordinal);
        Assert.Contains($"--bg-color: {theme.LightTokens["--bg-color"]};", html, StringComparison.Ordinal);
        Assert.Contains($"--accent:{theme.DarkTokens["--accent"]}", html, StringComparison.Ordinal);
    }

    // style-src represents only the nonce, so an unnonced override block would be blocked outright.
    [Fact]
    public void OverrideCss_CarriesTheNonce()
    {
        var css = Warden.Services.ThemeProvider.BuildThemeCss(
            new ThemeOptions { PrimaryColor = "#abc" }, "n0nce");

        Assert.Contains("<style nonce=\"n0nce\">", css, StringComparison.Ordinal);
    }

    private static string Render(IWardenTheme theme, string? nonce = null) =>
        LayoutProvider.GetLayout(title: "Test", content: "<p>body</p>", nonce: nonce, theme: theme);

    private static string RenderStyleBlock(IWardenTheme theme)
    {
        var themeTokenCss = ThemeCssBuilder.BuildTokenCss(theme, ThemeMode.Auto);
        return LayoutProvider.GetStylesAsset(themeTokenCss, theme.ComponentCss, "").Body;
    }

    [GeneratedRegex(@"(--[a-z0-9-]+)\s*:")]
    private static partial Regex DefinitionPattern();

    [GeneratedRegex(@"var\((--[a-z0-9-]+)")]
    private static partial Regex ReferencePattern();

    [GeneratedRegex(@"<link rel=""stylesheet"" href=""[^""]*""")]
    private static partial Regex StylesheetLinkPattern();

    [GeneratedRegex(@"([.\#][a-zA-Z0-9_-]+)\s*\{")]
    private static partial Regex ComponentCssFirstSelectorPattern();
}

/// <summary>Boots the real app so the whole chain runs: config.json to ThemeRegistry to the served HTML.</summary>
public sealed class ThemedSiteFactory : WardenWebApplicationFactory
{
    public string? ConfigTheme { get; init; }
    public string? OptionsTheme { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        if (ConfigTheme is not null)
            File.WriteAllText(Path.Combine(ContentDir, "config.json"), $"{{\"title\": \"Test Blog\", \"theme\": \"{ConfigTheme}\"}}");
        if (OptionsTheme is not null)
            builder.UseSetting("Docs:Themes:Name", OptionsTheme);
    }
}

public sealed class ThemeResolutionTests
{
    [Fact]
    public async Task ConfigJsonTheme_ReachesTheServedPage()
    {
        using var factory = new ThemedSiteFactory { ConfigTheme = "ocean" };
        var client = factory.CreateClient();
        var html = await client.GetStringAsync("/");
        var css = await client.GetStringAsync("/warden.css");

        Assert.Contains("class=\"theme-ocean\"", html, StringComparison.Ordinal);
        Assert.Contains("--bg-color: #f6fafc;", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownConfigJsonTheme_FallsBackToDefault()
    {
        using var factory = new ThemedSiteFactory { ConfigTheme = "not-a-theme" };
        var html = await factory.CreateClient().GetStringAsync("/");

        Assert.Contains("class=\"theme-default\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThemeOptionsName_OutranksConfigJson()
    {
        using var factory = new ThemedSiteFactory { ConfigTheme = "ocean", OptionsTheme = "solarized" };
        var html = await factory.CreateClient().GetStringAsync("/");

        Assert.Contains("class=\"theme-solarized\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoThemeConfigured_RendersTheDefaultDesign()
    {
        using var factory = new ThemedSiteFactory();
        var client = factory.CreateClient();
        var html = await client.GetStringAsync("/");
        var css = await client.GetStringAsync("/warden.css");

        Assert.Contains("class=\"theme-default\"", html, StringComparison.Ordinal);
        Assert.Contains("--accent: #2E4A36;", css, StringComparison.Ordinal);
    }
}
