namespace Warden.Services.Theming.Themes;

/// <summary>
/// Clean white paper, near-black type, bright blue accent; native dark mode is deep navy.
/// White / #030620, primary #0161EF, dark links #60A5FA).
/// </summary>
public sealed class MidnightTheme : IWardenTheme
{
    public string Name => "midnight";

    public string Label => "Midnight";

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
        ["--bg-color"] = "#030620",
        ["--sidebar-bg"] = "#0A0E2E",
        ["--text-color"] = "#E5ECF6",
        ["--text-muted"] = "#99A6C0",
        ["--accent"] = "#60A5FA",
        ["--accent-light"] = "#0F1838",
        ["--border"] = "rgba(255, 255, 255, 0.10)",
        ["--code-bg"] = "#0B0F2A"
    };

    public string ComponentCss => string.Empty;
}
