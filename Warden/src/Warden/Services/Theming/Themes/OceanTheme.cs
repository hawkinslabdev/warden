namespace Warden.Services.Theming.Themes;

/// <summary>Cool blue-grey paper, deep harbour accent, tinted lead band.</summary>
public sealed class OceanTheme : IWardenTheme
{
    public string Name => "ocean";

    public string Label => "Ocean";

    public IReadOnlyDictionary<string, string> LightTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#f6fafc",
        ["--sidebar-bg"] = "#e9f1f6",
        ["--text-color"] = "#0d2130",
        ["--text-muted"] = "#48626f",
        ["--accent"] = "#0a6382",
        ["--accent-light"] = "#e0edf4",
        ["--border"] = "#cfe0ea",
        ["--code-bg"] = "#edf4f8"
    };

    public IReadOnlyDictionary<string, string> DarkTokens { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--bg-color"] = "#0a1620",
        ["--sidebar-bg"] = "#0f1e2b",
        ["--text-color"] = "#dfeaf2",
        ["--text-muted"] = "#8ba6b9",
        ["--accent"] = "#4fb3d9",
        ["--accent-light"] = "#12293a",
        ["--border"] = "#1d3242",
        ["--code-bg"] = "#0d1c28"
    };

    public string ComponentCss => """
                .lead {
                    background-color: var(--accent-light);
                    border-bottom: 0;
                    border-radius: 12px;
                    padding: 2rem clamp(1.25rem, 3vw, 2rem) 2.25rem;
                }
                .lead-cover {
                    background-color: var(--bg-color);
                    border-radius: 10px;
                }
                .card-cover {
                    border-radius: 10px;
                }
                .content .lead-title {
                    color: var(--accent);
                }
                .tag-chip {
                    background-color: var(--bg-color);
                }
                .post-kicker {
                    display: block;
                    color: var(--accent);
                    font-size: 0.8rem;
                    font-weight: 700;
                    margin: 0.3rem 0 0.5rem;
                }
        """;
}
