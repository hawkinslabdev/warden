namespace Warden.Services.Theming.Themes;

/// <summary>
/// Identical to <see cref="MidnightTheme"/>, but uses pure black (#000000) for OLED dark mode.
/// </summary>
public sealed class OledTheme : IWardenTheme
{
    public string Name => "oled";

    public string Label => "OLED";

    public IReadOnlyDictionary<string, string> LightTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#FFFFFF",
        ["--sidebar-bg"] = "#F4F5F7",
        ["--text-color"] = "#101010",
        ["--text-muted"] = "#616161",
        ["--accent"] = "#0161EF",
        ["--accent-light"] = "#E8F0FE",
        ["--border"] = "rgba(0, 0, 0, 0.08)",
        ["--code-bg"] = "#F6F8FA"
    };

    public IReadOnlyDictionary<string, string> DarkTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#000000",
        ["--sidebar-bg"] = "#0A0A0A",
        ["--text-color"] = "#E5ECF6",
        ["--text-muted"] = "#99A6C0",
        ["--accent"] = "#60A5FA",
        ["--accent-light"] = "#101418",
        ["--border"] = "rgba(255, 255, 255, 0.12)",
        ["--code-bg"] = "#0D0D0D"
    };

    public string ComponentCss => string.Empty;
}
