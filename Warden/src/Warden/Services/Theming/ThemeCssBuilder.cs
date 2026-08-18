using System.Text;
using Warden.Models;

namespace Warden.Services.Theming;

/// <summary>Renders a theme's token dictionaries into the <c>:root</c> and dark-mode CSS blocks.</summary>
public static class ThemeCssBuilder
{
    /// <summary>Dark tokens go in both the OS query and <c>[data-theme="dark"]</c>, so the toggle wins over the OS.</summary>
    public static string BuildTokenCss(IWardenTheme theme, ThemeMode mode)
    {
        var light = Merge(ThemeDefaults.Light, theme.LightTokens);
        var sb = new StringBuilder();

        sb.Append("        :root {\n            color-scheme: light;\n")
          .Append(RenderVars(light))
          .Append("        }\n");

        if (mode == ThemeMode.Light)
            return sb.ToString();

        var dark = "            color-scheme: dark;\n" + RenderVars(Merge(ThemeDefaults.Dark, theme.DarkTokens));

        if (mode == ThemeMode.Auto)
        {
            sb.Append("        @media (prefers-color-scheme: dark) {\n")
              .Append("            :root:not([data-theme=\"light\"]) {\n").Append(dark).Append("            }\n")
              .Append("        }\n");
        }

        sb.Append("        :root[data-theme=\"dark\"] {\n").Append(dark).Append("        }\n");

        return sb.ToString();
    }

    /// <summary>The four tokens <c>Get404Layout</c> inlines in its own miniature stylesheet.</summary>
    public static string BuildMinimalTokenCss(IWardenTheme theme)
    {
        var light = Merge(ThemeDefaults.Light, theme.LightTokens);
        var dark = Merge(light, Merge(ThemeDefaults.Dark, theme.DarkTokens));
        return string.Join(";", MinimalKeys.Select(k => $"{k}:{dark[k]}"));
    }

    /// <summary>Light values for the 404 page's own <c>:root</c> block.</summary>
    public static string BuildMinimalLightTokenCss(IWardenTheme theme)
    {
        var light = Merge(ThemeDefaults.Light, theme.LightTokens);
        return string.Join("", MinimalKeys.Select(k => $"            {k}: {light[k]};\n"));
    }

    private static readonly string[] MinimalKeys = ["--bg-color", "--text-color", "--text-muted", "--accent"];

    private static Dictionary<string, string> Merge(
        IReadOnlyDictionary<string, string> baseTokens,
        IReadOnlyDictionary<string, string> overrides)
    {
        var merged = new Dictionary<string, string>(baseTokens, StringComparer.Ordinal);
        foreach (var (key, value) in overrides)
            merged[key] = value;
        return merged;
    }

    private static string RenderVars(Dictionary<string, string> tokens)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in tokens.OrderBy(t => t.Key, StringComparer.Ordinal))
            sb.Append("            ").Append(key).Append(": ").Append(value).Append(";\n");
        return sb.ToString();
    }
}
