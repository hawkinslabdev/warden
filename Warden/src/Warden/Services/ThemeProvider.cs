using Warden.Models;
using Warden.Services.Layout;

namespace Warden.Services;

public static class ThemeProvider
{
    public static string BuildThemeCss(ThemeOptions? theme, string? nonce = null)
    {
        if (theme is null)
            return string.Empty;

        var vars = new List<string>();

        AddVar(vars, "--primary-color", theme.PrimaryColor);
        AddVar(vars, "--bg-color", theme.BgColor);
        AddVar(vars, "--sidebar-bg", theme.SidebarBg);
        AddVar(vars, "--text-color", theme.TextColor);
        AddVar(vars, "--text-muted", theme.TextMuted);
        AddVar(vars, "--border", theme.BorderColor);
        AddVar(vars, "--code-bg", theme.CodeBg);
        AddVar(vars, "--accent-light", theme.AccentLight);
        AddVar(vars, "--font-sans", theme.FontSans);
        AddVar(vars, "--font-mono", theme.FontMono);

        if (vars.Count == 0)
            return string.Empty;

        var nonceAttr = nonce is { Length: > 0 } ? $" nonce=\"{nonce}\"" : string.Empty;
        return $"<style{nonceAttr}>\n:root {{\n" + string.Join("\n", vars) + "\n}\n</style>";
    }

    public static string BuildCustomCssLink(ThemeOptions? theme, string themeDir, string basePath = "")
    {
        var url = theme?.CustomCssUrl is { Length: > 0 } configured
            ? LayoutProvider.ResolveAssetUrl(configured, basePath)
            : (File.Exists(Path.Combine(themeDir, "custom.css")) ? $"{basePath}/theme/custom.css" : null);
        return url is { Length: > 0 }
            ? $"<link rel=\"stylesheet\" href=\"{url}\">"
            : string.Empty;
    }

    public static string BuildCustomJsScript(ThemeOptions? theme, string themeDir, string basePath = "")
    {
        var url = theme?.CustomJsUrl is { Length: > 0 } configured
            ? LayoutProvider.ResolveAssetUrl(configured, basePath)
            : (File.Exists(Path.Combine(themeDir, "custom.js")) ? $"{basePath}/theme/custom.js" : null);
        return url is { Length: > 0 }
            ? $"<script defer src=\"{url}\"></script>"
            : string.Empty;
    }

    public static string GetBrandText(ThemeOptions? theme)
    {
        if (theme?.BrandText is { Length: > 0 } brand)
            return System.Net.WebUtility.HtmlEncode(brand);
        return "Warden";
    }

    public static ThemeMode ResolveMode(ThemeOptions? theme) =>
        theme?.Mode?.Trim().ToLowerInvariant() switch
        {
            "dark" => ThemeMode.Dark,
            "light" => ThemeMode.Light,
            "auto" => ThemeMode.Auto,
            _ => (theme?.DarkMode ?? true) ? ThemeMode.Auto : ThemeMode.Light,
        };

    public static bool ShowScrollIndicator(ThemeOptions? theme) => theme?.ShowScrollIndicator ?? true;

    private static void AddVar(List<string> vars, string name, string? value)
    {
        if (value is { Length: > 0 })
            vars.Add($"    {name}: {value};");
    }
}
