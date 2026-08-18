namespace Warden.Services.Theming.Themes;

/// <summary>Solarized base tones: warm paper, deep teal night. Light text and accent deviate to clear 4.5:1.</summary>
public sealed class SolarizedTheme : IWardenTheme
{
    public string Name => "solarized";

    public string Label => "Solarized";

    public IReadOnlyDictionary<string, string> LightTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#fdf6e3",
        ["--sidebar-bg"] = "#eee8d5",
        ["--text-color"] = "#3a372f",
        ["--text-muted"] = "#5f5a4d",
        ["--accent"] = "#0f6795",
        ["--accent-light"] = "#e9e2cd",
        ["--border"] = "#ddd6c1",
        ["--code-bg"] = "#eee8d5"
    };

    public IReadOnlyDictionary<string, string> DarkTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#002b36",
        ["--sidebar-bg"] = "#073642",
        ["--text-color"] = "#d6e2e2",
        ["--text-muted"] = "#8fa3a3",
        ["--accent"] = "#4aa3dc",
        ["--accent-light"] = "#0a3a47",
        ["--border"] = "#12414e",
        ["--code-bg"] = "#04303c"
    };

    public string ComponentCss => """
                .lead-cover,
                .card-cover {
                    border-radius: 4px;
                }
                .tag-chip {
                    border-radius: 4px;
                }
                .post-kicker {
                    display: block;
                    color: var(--accent);
                    font-size: 0.8rem;
                    font-weight: 700;
                    margin: 0.3rem 0 0.5rem;
                }
                .prose div[class^="language-"] {
                    border-radius: 4px;
                }
                .prose blockquote {
                    border-radius: 0;
                }
        """;
}
