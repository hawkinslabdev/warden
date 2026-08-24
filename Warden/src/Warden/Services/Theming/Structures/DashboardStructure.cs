namespace Warden.Services.Theming.Structures;

/// <summary>
/// Card-grid status page: monitors as cards (status dot, badge, uptime, response-time chart, 90-day
/// history bar), optionally sectioned via <c>monitoring.group</c>, with an ongoing-incidents
/// panel pinned above the grid and a wider content column to fit it. Every other page (About, Guide,
/// incident detail) renders exactly like "clean" - this only changes the status page.
/// </summary>
public sealed class DashboardStructure : IWardenStructure
{
    public string Name => "dashboard";

    public string Label => "Dashboard";

    public bool UseCardStatusLayout => true;

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
                .content .status-monitor-grid {
                    list-style: none;
                    margin: 0;
                    padding: 0;
                    display: grid;
                    grid-template-columns: repeat(auto-fill, minmax(min(300px, 100%), 1fr));
                    gap: 1rem;
                }
                .content .status-monitor-card {
                    display: flex;
                    flex-direction: column;
                    gap: 0.5rem;
                    margin: 0;
                    padding: 1rem 1.1rem;
                    border: 1px solid var(--border);
                    border-radius: 10px;
                    background: var(--bg-color);
                    transition: border-color 0.15s ease;
                }
                .status-monitor-card--down {
                    border-color: color-mix(in srgb, var(--alert-caution) 40%, var(--border));
                    background: color-mix(in srgb, var(--alert-caution) 4%, var(--bg-color));
                }
                .status-monitor-card--maintenance {
                    border-color: color-mix(in srgb, var(--alert-note) 40%, var(--border));
                    background: color-mix(in srgb, var(--alert-note) 4%, var(--bg-color));
                }
                .status-monitor-card--degraded {
                    border-color: color-mix(in srgb, var(--alert-warning) 40%, var(--border));
                    background: color-mix(in srgb, var(--alert-warning) 4%, var(--bg-color));
                }
                .status-monitor-card-head {
                    display: flex;
                    align-items: center;
                    gap: 0.4rem 0.55rem;
                }
                .status-monitor-dot {
                    flex: none;
                    width: 9px;
                    height: 9px;
                    border-radius: 50%;
                    background: var(--text-muted);
                }
                .status-monitor-card--up .status-monitor-dot { background: var(--alert-tip); }
                .status-monitor-card--down .status-monitor-dot { background: var(--alert-caution); }
                .status-monitor-card--maintenance .status-monitor-dot { background: var(--alert-note); }
                .status-monitor-card--degraded .status-monitor-dot { background: var(--alert-warning); }
                .status-monitor-card .status-monitor-name {
                    min-width: 0;
                    overflow: hidden;
                    text-overflow: ellipsis;
                    white-space: nowrap;
                }
                .status-monitor-card .status-monitor-badge {
                    flex: none;
                }
                .status-monitor-card--up .status-monitor-badge {
                    background: color-mix(in srgb, var(--alert-tip) 18%, transparent);
                    color: var(--alert-tip);
                }
                .status-monitor-card--down .status-monitor-badge {
                    background: color-mix(in srgb, var(--alert-caution) 18%, transparent);
                    color: var(--alert-caution);
                }
                .status-monitor-card--unknown .status-monitor-badge {
                    background: color-mix(in srgb, var(--text-muted) 20%, transparent);
                    color: var(--text-muted);
                }
                .status-monitor-card--maintenance .status-monitor-badge {
                    background: color-mix(in srgb, var(--alert-note) 18%, transparent);
                    color: var(--alert-note);
                }
                .status-monitor-card--degraded .status-monitor-badge {
                    background: color-mix(in srgb, var(--alert-warning) 18%, transparent);
                    color: var(--alert-warning);
                }
                .status-response-chart {
                    display: flex;
                    align-items: flex-end;
                    gap: 1px;
                    width: 100%;
                    height: 26px;
                    overflow: hidden;
                    margin-top: 0.15rem;
                }
                .status-response-bar {
                    height: calc(max(3px, var(--bar-h, 0) * 100%));
                    background: var(--accent);
                    opacity: 0.55;
                    border-radius: 1px;
                }
                /* a card grid needs more room than the site's narrow reading column; every other
                   page (no .status-monitor-grid present) keeps that column untouched */
                .main-container:has(.status-monitor-grid) {
                    max-width: min(1200px, 96vw);
                }
                .content.reading:has(.status-monitor-grid) {
                    max-width: none;
                }
                """;
}
