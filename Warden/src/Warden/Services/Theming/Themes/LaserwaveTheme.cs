namespace Warden.Services.Theming.Themes;

/// <summary>Synthwave violet, hot magenta accent.</summary>
public sealed class LaserwaveTheme : IWardenTheme
{
    public string Name => "laserwave";

    public string Label => "Laserwave";

    public IReadOnlyDictionary<string, string> LightTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#faf7fb",
        ["--sidebar-bg"] = "#f2ecf5",
        ["--text-color"] = "#241d2b",
        ["--text-muted"] = "#5f5369",
        ["--accent"] = "#a3186e",
        ["--accent-light"] = "#f4e7f0",
        ["--border"] = "#e3d9e9",
        ["--code-bg"] = "#f4eff7"
    };

    public IReadOnlyDictionary<string, string> DarkTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#1e1a24",
        ["--sidebar-bg"] = "#27212e",
        ["--text-color"] = "#e6e2e8",
        ["--text-muted"] = "#a599b0",
        ["--accent"] = "#eb64b9",
        ["--accent-light"] = "#33263a",
        ["--border"] = "#3b3145",
        ["--code-bg"] = "#241f2b"
    };

    public string ComponentCss => """
                .post-card {
                    border-bottom: 0;
                    border-top: 2px solid var(--accent);
                    padding: 1.6rem 0 2.15rem;
                }
                .lead {
                    border-bottom-width: 2px;
                    border-bottom-color: var(--accent);
                }
                .post-card-title,
                .content .lead-title {
                    letter-spacing: -0.035em;
                }
                .tag-chip {
                    border-radius: 3px;
                    letter-spacing: 0.06em;
                    text-transform: uppercase;
                }
                .post-kicker {
                    display: block;
                    color: var(--accent);
                    text-transform: uppercase;
                    letter-spacing: 0.06em;
                    font-size: 0.75rem;
                    font-weight: 700;
                    margin: 0.3rem 0 0.5rem;
                }
        """;
}
