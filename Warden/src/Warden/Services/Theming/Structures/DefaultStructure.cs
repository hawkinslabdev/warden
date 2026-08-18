namespace Warden.Services.Theming.Structures;

/// <summary>Dashboard's status-page header (overall uptime line, pinned ongoing-incidents panel) on top of the plain flat monitor list - everything else renders exactly like "clean".</summary>
public sealed class DefaultStructure : IWardenStructure
{
    public string Name => "default";

    public string Label => "Default";

    public bool ShowStatusHeader => true;

    public string ComponentCss => """
                .content .status-overall-uptime {
                    margin: 0 0 2rem;
                    font-size: 1.3rem;
                    font-weight: 600;
                    font-family: var(--font-display);
                    letter-spacing: -0.01em;
                    color: var(--text-color);
                    font-variant-numeric: tabular-nums;
                }
                .status-ongoing-incidents {
                    margin: 0 0 2.5rem;
                }
                .status-ongoing-incidents .status-group-heading {
                    color: var(--alert-caution);
                }
                """;
}
