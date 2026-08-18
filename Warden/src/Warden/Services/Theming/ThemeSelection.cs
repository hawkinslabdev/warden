using Warden.Models;
using Warden.Services;

namespace Warden.Services.Theming;

/// <summary>
/// Resolves the palette and the light/dark mode from one theme value, so <c>"solarized dark"</c> or a bare <c>"dark"</c> both work wherever a theme is named.
/// </summary>
public readonly record struct ThemeSelection(IWardenTheme Theme, ThemeMode Mode)
{
    /// <summary>
    /// Highest source that names a palette wins for the palette, and likewise for the mode, so appsettings can pin the palette while <c>config.json</c> pins the mode.
    /// </summary>
    public static ThemeSelection Resolve(ThemeOptions? options, string? cliTheme, string? configTheme)
    {
        string? name = null;
        ThemeMode? mode = null;

        foreach (var value in (string?[])[cliTheme, options?.Name, configTheme])
        {
            var (valueName, valueMode) = Split(value);
            name ??= valueName;
            mode ??= valueMode;
        }

        return new ThemeSelection(ThemeRegistry.Resolve(name), mode ?? ThemeProvider.ResolveMode(options));
    }

    internal static (string? Name, ThemeMode? Mode) Split(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (null, null);

        string? name = null;
        ThemeMode? mode = null;

        foreach (var token in value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            switch (token.ToLowerInvariant())
            {
                case "dark": mode ??= ThemeMode.Dark; break;
                case "light": mode ??= ThemeMode.Light; break;
                case "auto": mode ??= ThemeMode.Auto; break;
                default: name ??= token; break;
            }
        }

        return (name, mode);
    }
}
