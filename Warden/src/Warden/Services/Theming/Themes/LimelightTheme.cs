namespace Warden.Services.Theming.Themes;

/// <summary>Pale off-white ground, sage-lime accent with a cyan counterpart.</summary>
public sealed class LimelightTheme : IWardenTheme
{
    public string Name => "limelight";

    public string Label => "Limelight";

    public IReadOnlyDictionary<string, string> LightTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#f7f8f4",
        ["--sidebar-bg"] = "#eff2e9",
        ["--text-color"] = "#1d201b",
        ["--text-muted"] = "#5b6155",
        ["--accent"] = "#42700e",
        ["--accent-alt"] = "#0b6a86",
        ["--accent-light"] = "#e8eedb",
        ["--border"] = "#dee3d3",
        ["--code-bg"] = "#f1f4ec"
    };

    public IReadOnlyDictionary<string, string> DarkTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#12140f",
        ["--sidebar-bg"] = "#191c15",
        ["--text-color"] = "#dde1d6",
        ["--text-muted"] = "#969c8c",
        ["--accent"] = "#9fbe3f",
        ["--accent-alt"] = "#67b9d8",
        ["--accent-light"] = "#1f2618",
        ["--border"] = "#292e21",
        ["--code-bg"] = "#181b14"
    };

    public string ComponentCss => """
                .post-card,
                .lead {
                    border-bottom-color: var(--border);
                }
                .tag-chip {
                    text-transform: uppercase;
                    letter-spacing: 0.08em;
                    font-size: 0.66rem;
                }
                .post-kicker {
                    display: block;
                    color: var(--accent);
                    text-transform: uppercase;
                    letter-spacing: 0.08em;
                    font-size: 0.7rem;
                    font-weight: 700;
                    margin: 0.3rem 0 0.5rem;
                }
                .list-heading {
                    letter-spacing: -0.03em;
                }
                /* The cyan-to-lime pair reads as one mark rather than two accents. */
                #scroll-indicator {
                    background: linear-gradient(90deg, var(--accent-alt), var(--accent));
                }
        """;
}
