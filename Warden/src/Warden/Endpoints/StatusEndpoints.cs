using Warden.Models;
using Warden.Services;
using Warden.Services.Layout;
using Warden.Services.Rendering;

namespace Warden.Endpoints;

internal static class StatusEndpoints
{
    private static readonly TimeSpan UptimeWindow = TimeSpan.FromHours(24);
    private const int HistoryDays = 90;

    // exact routes outrank the content catch-all, so this always wins "/" regardless of registration order
    public static IEndpointRouteBuilder MapStatusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapMethods("/", HttpVerbs.GetAndHead, RenderStatus);
        return app;
    }

    private static async Task RenderStatus(HttpContext ctx, HeartbeatStore store, ContentService content, PageResponder responder)
    {
        var l = Localization.Current;
        var monitoring = content.SiteConfig?.Monitoring;
        var targets = monitoring?.Targets ?? [];
        var filterDay = ParseFilterDay(ctx.Request.Query["on"]);
        var html = targets.Count == 0
            ? $"<p class=\"status-unavailable\">{LayoutProvider.HtmlEncode(l.StatusUnavailable)}</p>"
            : BuildStatusHtml(store, targets, await content.GetAllPagesAsync(ctx.RequestAborted), monitoring, filterDay, responder.BasePath);

        await responder.WriteAsync(ctx, new PageView(
            Title: l.StatusPageTitle,
            ContentHtml: $"<header class=\"page-header\"><h1 class=\"page-title\">{LayoutProvider.HtmlEncode(l.StatusPageTitle)}</h1></header>" + html,
            CanonicalPath: "",
            Prose: true,
            NoIndex: filterDay is not null));
    }

    private static DateOnly? ParseFilterDay(string? raw) =>
        DateOnly.TryParseExact(raw, "yyyy-MM-dd", out var day) ? day : null;

    private static string BuildStatusHtml(HeartbeatStore store, IReadOnlyList<MonitorTarget> targets, IReadOnlyList<DocumentationPage> pages, MonitoringConfig? monitoring, DateOnly? filterDay, string basePath)
    {
        var l = Localization.Current;
        var incidentMonitorIds = IncidentContent.ActiveIncidentMonitorIds(pages);
        var maintainedIds = IncidentContent.ActiveMaintenanceMonitorIds(pages, DateTimeOffset.UtcNow);
        var statuses = targets.ToDictionary(t => t.Id, t => IncidentContent.StatusOverride(t.Id, incidentMonitorIds, maintainedIds) ?? LatestStatus(store, t.Id));
        var anyDown = statuses.Values.Any(s => s == MonitorStatus.Down);

        var sb = new System.Text.StringBuilder();
        sb.Append("<p class=\"status-banner status-banner--")
          .Append(anyDown ? "down" : "up").Append(" select-none\">")
          .Append(LayoutProvider.HtmlEncode(anyDown ? l.StatusSomeDown : l.StatusAllOperational))
          .Append("</p>");

        sb.Append("<section class=\"status-group\"><ul class=\"status-monitor-list\">");
        foreach (var target in targets)
        {
            var status = statuses[target.Id];
            var uptime = store.GetUptime(target.Id, UptimeWindow);
            sb.Append("<li class=\"status-monitor status-monitor--").Append(StatusClass(status)).Append("\">")
              .Append("<span class=\"status-monitor-name\">").Append(LayoutProvider.HtmlEncode(target.Name)).Append("</span>")
              .Append("<span class=\"status-monitor-badge select-none\">").Append(LayoutProvider.HtmlEncode(StatusLabel(l, status))).Append("</span>");
            if (uptime is { } u)
                sb.Append("<span class=\"status-monitor-uptime select-none\">").Append(LayoutProvider.HtmlEncode(l.StatusUptimeLabel(u.Percent, FormatDuration(u.Span)))).Append("</span>");
            sb.Append(BuildHistoryBar(store, target.Id, basePath));
            sb.Append("</li>");
        }
        sb.Append("</ul></section>");

        if (filterDay is { } day)
            sb.Append("<p class=\"status-filter\"><span>").Append(LayoutProvider.HtmlEncode(l.StatusFilterShowing(DateFormatter.Current.Medium(day.ToDateTime(TimeOnly.MinValue)))))
              .Append("</span><a href=\"").Append(basePath).Append("/#status-incidents\" class=\"status-filter-clear\">")
              .Append(LayoutProvider.HtmlEncode(l.StatusFilterClear)).Append("</a></p>");

        BuildIncidentsSection(sb, l, pages, monitoring, filterDay, basePath);
        BuildMaintenanceSection(sb, l, pages, monitoring, filterDay, basePath);

        return sb.ToString();
    }

    // hand-authored content/incidents/*.md; ongoing and recently-resolved both show, newest first
    private static void BuildIncidentsSection(System.Text.StringBuilder sb, Localization l, IReadOnlyList<DocumentationPage> pages, MonitoringConfig? monitoring, DateOnly? filterDay, string basePath)
    {
        var maxShown = monitoring?.IncidentMaxShown ?? IncidentContent.DefaultIncidentMaxShown;
        var incidents = filterDay is { } day
            ? IncidentContent.IncidentsOnDay(pages, day, maxShown)
            : IncidentContent.RecentIncidents(pages, DateTimeOffset.UtcNow, monitoring?.IncidentWindowDays ?? IncidentContent.DefaultIncidentWindowDays, maxShown);

        sb.Append("<section class=\"status-incidents\"><h2 class=\"status-group-heading select-none\">")
          .Append(LayoutProvider.HtmlEncode(l.StatusIncidentsHeading)).Append("</h2>");
        if (incidents.Count == 0)
        {
            sb.Append("<p class=\"status-no-incidents\">").Append(LayoutProvider.HtmlEncode(l.StatusNoIncidents)).Append("</p>");
            sb.Append("</section>");
            return;
        }

        foreach (var page in incidents)
        {
            var resolved = page.End is not null;
            var badgeClass = IncidentContent.IncidentBadgeClass(page);
            var content = resolved
                ? l.StatusOutagePeriod(IncidentContent.TimeHtml(IncidentContent.StartOf(page)), IncidentContent.TimeHtml(IncidentContent.EndOf(page)!.Value))
                : l.StatusDownSince(IncidentContent.TimeHtml(IncidentContent.StartOf(page)));
            sb.Append("<article class=\"status-incident\"><div class=\"status-incident-head\"><h3 class=\"status-incident-title\"><a href=\"")
              .Append(UrlPaths.Href(basePath, page.Path)).Append("\">")
              .Append(LayoutProvider.HtmlEncode(page.Title)).Append("</a></h3>")
              .Append("<span class=\"status-incident-badge status-incident-badge--").Append(badgeClass).Append("\">")
              .Append(LayoutProvider.HtmlEncode(resolved ? l.StatusResolved : l.StatusDown)).Append("</span>")
              .Append("</div><div class=\"status-incident-content\">")
              .Append(content)
              .Append("</div></article>");
        }
        sb.Append("</section>");
    }

    // hand-authored content/incidents/*.md with maintenance: true; drops off once End passes
    private static void BuildMaintenanceSection(System.Text.StringBuilder sb, Localization l, IReadOnlyList<DocumentationPage> pages, MonitoringConfig? monitoring, DateOnly? filterDay, string basePath)
    {
        var now = DateTimeOffset.UtcNow;
        var maxShown = monitoring?.MaintenanceMaxShown ?? IncidentContent.DefaultMaintenanceMaxShown;
        var upcoming = filterDay is { } day
            ? IncidentContent.MaintenanceOnDay(pages, day, maxShown)
            : IncidentContent.UpcomingMaintenance(pages, now, monitoring?.MaintenanceWindowDays ?? IncidentContent.DefaultMaintenanceWindowDays, maxShown);
        if (upcoming.Count == 0) return;

        sb.Append("<section class=\"status-maintenance\"><h2 class=\"status-group-heading select-none\">")
          .Append(LayoutProvider.HtmlEncode(l.StatusMaintenanceHeading)).Append("</h2>");
        foreach (var page in upcoming)
        {
            var start = IncidentContent.StartOf(page);
            var end = IncidentContent.EndOf(page)!.Value;
            var badgeClass = IncidentContent.MaintenanceBadgeClass(page, now);
            var badgeText = badgeClass == "active" ? l.StatusMaintenanceActive : l.StatusMaintenancePlanned;
            sb.Append("<article class=\"status-maintenance-item status-maintenance-item--").Append(badgeClass).Append("\">")
              .Append("<div class=\"status-maintenance-head\"><h3 class=\"status-maintenance-title\"><a href=\"")
              .Append(UrlPaths.Href(basePath, page.Path)).Append("\">")
              .Append(LayoutProvider.HtmlEncode(page.Title)).Append("</a></h3><span class=\"status-maintenance-badge status-maintenance-badge--").Append(badgeClass).Append("\">")
              .Append(LayoutProvider.HtmlEncode(badgeText))
              .Append("</span></div><div class=\"status-maintenance-window\">")
              .Append(l.StatusMaintenanceWindow(IncidentContent.TimeHtml(start), IncidentContent.TimeHtml(end)))
              .Append("</div>");
            if (page.Description is { Length: > 0 } description)
                sb.Append("<p class=\"status-maintenance-description\">").Append(LayoutProvider.HtmlEncode(description)).Append("</p>");
            sb.Append("</article>");
        }
        sb.Append("</section>");
    }

    private static string FormatDuration(TimeSpan span) => span switch
    {
        { TotalDays: >= 1 } => $"{(int)span.TotalDays}d",
        { TotalHours: >= 1 } => $"{(int)span.TotalHours}h",
        _ => $"{Math.Max(1, (int)span.TotalMinutes)}m",
    };

    // one tick per calendar day over HistoryDays, like the 90-day bars on GitHub/Better Uptime style status pages;
    // each tick links to ?on=<day>, so a reader can pull up whatever incidents/maintenance happened that day even outside the default window
    private static string BuildHistoryBar(HeartbeatStore store, string monitorId, string basePath)
    {
        var l = Localization.Current;
        var days = store.GetDailyStatus(monitorId, HistoryDays);
        var sb = new System.Text.StringBuilder("<div class=\"status-monitor-bar\">");

        foreach (var day in days)
        {
            var cls = day.Status switch { MonitorStatus.Up => "up", MonitorStatus.Down => "down", _ => "unknown" };
            var label = day.Status switch { MonitorStatus.Up => l.StatusOperational, MonitorStatus.Down => l.StatusDown, _ => l.StatusNoData };
            var tip = $"{label} · {DateFormatter.Current.Medium(day.Day.ToDateTime(TimeOnly.MinValue))}";
            sb.Append("<a href=\"").Append(basePath).Append("/?on=").Append(day.Day.ToString("yyyy-MM-dd"))
              .Append("#status-incidents\" class=\"status-tick status-tick--").Append(cls).Append("\" data-tip=\"")
              .Append(LayoutProvider.HtmlEncode(tip)).Append("\"></a>");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    private static MonitorStatus LatestStatus(HeartbeatStore store, string monitorId) =>
        store.GetLatest(monitorId) is { } beat ? (beat.Data.Up ? MonitorStatus.Up : MonitorStatus.Down) : MonitorStatus.Unknown;

    private static string StatusClass(MonitorStatus status) => status switch
    {
        MonitorStatus.Up => "up",
        MonitorStatus.Down => "down",
        MonitorStatus.Maintenance => "maintenance",
        _ => "unknown",
    };

    private static string StatusLabel(Localization l, MonitorStatus status) => status switch
    {
        MonitorStatus.Up => l.StatusOperational,
        MonitorStatus.Down => l.StatusDown,
        MonitorStatus.Maintenance => l.StatusMonitorMaintenance,
        _ => l.StatusUnknown,
    };
}
