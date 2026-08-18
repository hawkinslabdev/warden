namespace Warden.Services.Theming.Themes;

/// <summary>White paper, near-black type, violet accent. Pair with <c>structure: editorial</c> for the matching page shape.</summary>
public sealed class CasperTheme : IWardenTheme
{
    public string Name => "casper";

    public string Label => "Casper";

    public IReadOnlyDictionary<string, string> LightTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#FFFFFF",
        ["--sidebar-bg"] = "#F4F4F5",
        ["--text-color"] = "#15171A",
        ["--text-muted"] = "#6E7173",
        ["--accent"] = "#7137C8",
        ["--accent-light"] = "#EEE7F8",
        ["--border"] = "rgba(0, 0, 0, 0.08)",
        ["--code-bg"] = "#EEF0F2"
    };

    public IReadOnlyDictionary<string, string> DarkTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#15171A",
        ["--sidebar-bg"] = "#1C1E22",
        ["--text-color"] = "#E5E7EB",
        ["--text-muted"] = "#9A9EA3",
        ["--accent"] = "#9C6ADE",
        ["--accent-light"] = "#292337",
        ["--border"] = "rgba(255, 255, 255, 0.10)",
        ["--code-bg"] = "#1E2024"
    };

    /// <summary>Empty like every other palette-only theme: the page shape lives in <see cref="Structures.EditorialStructure"/> now.</summary>
    public string ComponentCss => string.Empty;
}
