namespace Warden.Services.Theming.Themes;

/// <summary>Warm paper, forest green, no cover chrome. "The Reading Room". Used when none is configured.</summary>
public sealed class DefaultTheme : IWardenTheme
{
    public string Name => "default";

    public string Label => "Default";

    public IReadOnlyDictionary<string, string> LightTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#FBFAF7",
        ["--sidebar-bg"] = "#F3F1EA",
        ["--text-color"] = "#1B1D1A",
        ["--text-muted"] = "#5A5F58",
        ["--accent"] = "#2E4A36",
        ["--accent-light"] = "#E7ECE7",
        // Alpha, not a hex literal: the hairline has to sit on paper and on tinted cards alike.
        ["--border"] = "rgba(20, 24, 20, 0.09)",
        ["--code-bg"] = "#F3F1EA"
    };

    public IReadOnlyDictionary<string, string> DarkTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#0F1210",
        ["--sidebar-bg"] = "#171A17",
        ["--text-color"] = "#E7E9E4",
        ["--text-muted"] = "#9AA09A",
        ["--accent"] = "#7FA588",
        ["--accent-light"] = "#1E271F",
        ["--border"] = "rgba(255, 255, 255, 0.10)",
        ["--code-bg"] = "#171A17"
    };

    /// <summary>Empty: the base stylesheet is this theme.</summary>
    public string ComponentCss => string.Empty;
}
