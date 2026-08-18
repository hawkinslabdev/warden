namespace Warden.Services.Theming.Themes;

/// <summary>Near-black navy, periwinkle accent. Darkest built-in.</summary>
public sealed class DeepSpaceTheme : IWardenTheme
{
    public string Name => "deep-space";

    public string Label => "Deep Space";

    public IReadOnlyDictionary<string, string> LightTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#f7f8fc",
        ["--sidebar-bg"] = "#eceff8",
        ["--text-color"] = "#131832",
        ["--text-muted"] = "#4d5578",
        ["--accent"] = "#2f4fa8",
        ["--accent-light"] = "#e6eaf7",
        ["--border"] = "#d5dbee",
        ["--code-bg"] = "#eef1f9"
    };

    public IReadOnlyDictionary<string, string> DarkTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#070b1a",
        ["--sidebar-bg"] = "#0d1327",
        ["--text-color"] = "#dbe2f5",
        ["--text-muted"] = "#8b95bb",
        ["--accent"] = "#7aa2f7",
        ["--accent-light"] = "#141c38",
        ["--border"] = "#202a4a",
        ["--code-bg"] = "#0a1022"
    };

    public string ComponentCss => """
                .lead-cover {
                    border-radius: 16px;
                }
                .card-cover {
                    border-radius: 12px;
                }
                .content .lead-title {
                    color: var(--accent);
                }
                .post-meta {
                    text-transform: uppercase;
                    letter-spacing: 0.07em;
                    font-size: 0.75rem;
                }
                .post-kicker {
                    display: block;
                    color: var(--accent);
                    text-transform: uppercase;
                    letter-spacing: 0.07em;
                    font-size: 0.75rem;
                    font-weight: 600;
                    margin: 0.3rem 0 0.5rem;
                }
                .tag-chip {
                    border-radius: 6px;
                    text-transform: none;
                    letter-spacing: 0.02em;
                }
        """;
}
