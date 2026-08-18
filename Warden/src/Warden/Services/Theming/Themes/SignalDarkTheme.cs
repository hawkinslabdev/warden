namespace Warden.Services.Theming.Themes;

/// <summary>Deep charcoal, amber accent, monospaced meta lines.</summary>
public sealed class SignalDarkTheme : IWardenTheme
{
    public string Name => "signal-dark";

    public string Label => "Signal Dark";

    public IReadOnlyDictionary<string, string> LightTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#faf9f6",
        ["--sidebar-bg"] = "#f1f0ea",
        ["--text-color"] = "#1a1a15",
        ["--text-muted"] = "#5c5a4e",
        ["--accent"] = "#7d6114",
        ["--accent-light"] = "#f0ede1",
        ["--border"] = "#e2dfd4",
        ["--code-bg"] = "#f2f0e9"
    };

    public IReadOnlyDictionary<string, string> DarkTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#14140f",
        ["--sidebar-bg"] = "#1b1b14",
        ["--text-color"] = "#ebe7d6",
        ["--text-muted"] = "#9d9781",
        ["--accent"] = "#e0c65f",
        ["--accent-light"] = "#2a2617",
        ["--border"] = "#2f2d20",
        ["--code-bg"] = "#1c1b14"
    };

    public string ComponentCss => """
                .post-meta {
                    font-family: var(--font-mono);
                    font-size: 0.78rem;
                    letter-spacing: 0;
                }
                .post-card-title,
                .content .lead-title {
                    letter-spacing: -0.01em;
                }
                .post-kicker {
                    display: block;
                    color: var(--accent);
                    font-size: 0.78rem;
                    font-weight: 600;
                    margin: 0.3rem 0 0.5rem;
                }
                .readmore {
                    font-weight: 500;
                }
        """;
}
