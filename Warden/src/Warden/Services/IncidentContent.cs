using Warden.Models;
using Warden.Services.Layout;

namespace Warden.Services;

// content/incidents/*.md is scanned as ordinary pages by ContentService; this just filters/sorts that set for the status page and /api/status
internal static class IncidentContent
{
    private const string FolderPrefix = "incidents/";
    public const int DefaultIncidentWindowDays = 7;
    public const int DefaultIncidentMaxShown = 10;
    public const int DefaultMaintenanceWindowDays = 14;
    public const int DefaultMaintenanceMaxShown = 10;

    // Date represents the incident/maintenance start; YAML gives it Kind=Unspecified for a bare "Z" timestamp, so pin it to Utc before it becomes a DateTimeOffset
    public static DateTimeOffset StartOf(DocumentationPage page) =>
        new(DateTime.SpecifyKind(page.Date ?? default, DateTimeKind.Utc));

    public static DateTimeOffset? EndOf(DocumentationPage page) =>
        page.End is { } end ? new DateTimeOffset(DateTime.SpecifyKind(end, DateTimeKind.Utc)) : null;

    private static IEnumerable<DocumentationPage> InFolder(IReadOnlyList<DocumentationPage> pages, bool maintenance) =>
        pages.Where(p => p.OriginalRelativePath?.StartsWith(FolderPrefix, StringComparison.Ordinal) == true
                          && p.Maintenance == maintenance);

    // an ongoing incident always shows regardless of window; a resolved one ages out after windowDays, newest first
    public static List<DocumentationPage> RecentIncidents(IReadOnlyList<DocumentationPage> pages, DateTimeOffset now, int windowDays, int maxShown) =>
        [.. InFolder(pages, maintenance: false)
              .Where(p => EndOf(p) is not { } end || end >= now.AddDays(-windowDays))
              .OrderByDescending(StartOf)
              .Take(maxShown)];

    // an active window always shows; a planned one ages in once it's within windowDays of starting
    public static List<DocumentationPage> UpcomingMaintenance(IReadOnlyList<DocumentationPage> pages, DateTimeOffset now, int windowDays, int maxShown) =>
        [.. InFolder(pages, maintenance: true)
              .Where(p => EndOf(p) >= now && StartOf(p) <= now.AddDays(windowDays))
              .OrderBy(StartOf)
              .Take(maxShown)];

    // explicit lookup for one calendar day (via the history-bar tick filter); ignores the rolling window so older/further-out items are still findable
    public static List<DocumentationPage> IncidentsOnDay(IReadOnlyList<DocumentationPage> pages, DateOnly day, int maxShown) =>
        [.. InFolder(pages, maintenance: false)
              .Where(p => OverlapsDay(StartOf(p), EndOf(p) ?? DateTimeOffset.UtcNow, day))
              .OrderByDescending(StartOf)
              .Take(maxShown)];

    public static List<DocumentationPage> MaintenanceOnDay(IReadOnlyList<DocumentationPage> pages, DateOnly day, int maxShown) =>
        [.. InFolder(pages, maintenance: true)
              .Where(p => EndOf(p) is { } end && OverlapsDay(StartOf(p), end, day))
              .OrderBy(StartOf)
              .Take(maxShown)];

    private static bool OverlapsDay(DateTimeOffset start, DateTimeOffset end, DateOnly day)
    {
        var dayStart = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return start < dayStart.AddDays(1) && end >= dayStart;
    }

    // uptime-kuma-style: a monitor named in an active window's `monitors:` list shows Maintenance instead of Up/Down, and is excluded from the "some systems down" banner
    public static HashSet<string> ActiveMaintenanceMonitorIds(IReadOnlyList<DocumentationPage> pages, DateTimeOffset now)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in InFolder(pages, maintenance: true))
        {
            if (page.Monitors is not { Count: > 0 } monitors || MaintenanceBadgeClass(page, now) != "active")
                continue;
            foreach (var id in monitors)
                ids.Add(id);
        }
        return ids;
    }

    // same idea, for an unresolved incident's `monitors:` list; a declared incident is more authoritative than
    // the raw heartbeat (it covers what a simple up/down ping can't, like "API responses degraded"), so this
    // wins over both the heartbeat and an active maintenance window on the same monitor - see StatusOverride
    public static HashSet<string> ActiveIncidentMonitorIds(IReadOnlyList<DocumentationPage> pages)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var page in InFolder(pages, maintenance: false))
        {
            if (page.Monitors is not { Count: > 0 } monitors || page.End is not null)
                continue;
            foreach (var id in monitors)
                ids.Add(id);
        }
        return ids;
    }

    // precedence: an active incident beats an active maintenance window beats the real heartbeat
    public static MonitorStatus? StatusOverride(string monitorId, HashSet<string> incidentMonitorIds, HashSet<string> maintenanceMonitorIds) =>
        incidentMonitorIds.Contains(monitorId) ? MonitorStatus.Down
        : maintenanceMonitorIds.Contains(monitorId) ? MonitorStatus.Maintenance
        : null;

    public static string IncidentBadgeClass(DocumentationPage page) => page.End is null ? "down" : "resolved";

    public static string MaintenanceBadgeClass(DocumentationPage page, DateTimeOffset now)
    {
        var start = StartOf(page);
        var end = EndOf(page);
        return now < start ? "planned" : end is null || now <= end ? "active" : "ended";
    }

    // server-rendered UTC fallback (no-JS, screen readers before hydration); a script rewrites this to the viewer's own locale/timezone on load
    public static string TimeHtml(DateTimeOffset when)
    {
        var iso = when.ToString("O");
        var fallback = when.UtcDateTime.ToString("d MMM yyyy, HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
        return $"<span class=\"status-time\" data-iso=\"{iso}\">{LayoutProvider.HtmlEncode(fallback)}</span>";
    }
}
