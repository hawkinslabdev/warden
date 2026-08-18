using Warden.Services.Theming.Themes;

namespace Warden.Services.Theming;

/// <summary>Built-in themes. To add your own, implement <see cref="IWardenTheme"/> and add a line to <see cref="All"/>.</summary>
public static class ThemeRegistry
{
    public static IReadOnlyList<IWardenTheme> All { get; } =
    [
        new DefaultTheme(),
        new CasperTheme(),
        new OceanTheme(),
        new DeepSpaceTheme(),
        new SolarizedTheme(),
        new LaserwaveTheme(),
        new SignalDarkTheme(),
        new LimelightTheme(),
        new MidnightTheme(),
        new OledTheme()
    ];

    public static IWardenTheme Default { get; } = All[0];

    /// <summary>Unknown names warn and fall back to the default; a typo must never take a site down.</summary>
    public static IWardenTheme Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Default;

        var trimmed = name.Trim();
        foreach (var theme in All)
        {
            if (string.Equals(theme.Name, trimmed, StringComparison.OrdinalIgnoreCase))
                return theme;
        }

        Serilog.Log.Warning(
            "Unknown theme {Theme}; falling back to {Default}. Available: {Available}",
            trimmed, Default.Name, string.Join(", ", All.Select(t => t.Name)));
        return Default;
    }
}
