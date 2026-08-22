using Warden.Models;
using Warden.Services;
using Warden.Services.Layout;
using Warden.Services.Rendering;
using Warden.Services.Theming;

namespace Warden.Endpoints;

internal static class StatusEndpoints
{
    private static readonly TimeSpan UptimeWindow = TimeSpan.FromHours(24);
    private const int HistoryDays = 90;
    private const int ResponseChartDays = 30;

    // a type this dictionary doesn't know about (a custom deployment's own check) still gets a readable group heading
    private static readonly Dictionary<string, string> TypeGroupLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["http"] = "HTTP",
        ["service_backend"] = "Service health",
        ["ping"] = "Ping",
        ["tcp"] = "TCP",
        ["ftp"] = "FTP",
        ["sftp"] = "SFTP",
        ["database"] = "Database",
        ["dns"] = "DNS",
        ["ssl"] = "SSL certificates",
    };

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
        var targets = (monitoring?.Targets ?? []).Where(t => t.Hidden != true).ToList();
        var filterDay = ParseFilterDay(ctx.Request.Query["on"]);
        var structure = responder.ResolveStructure();
        var html = targets.Count == 0
            ? $"<p class=\"status-unavailable\">{LayoutProvider.HtmlEncode(l.StatusUnavailable)}</p>"
            : BuildStatusHtml(store, targets, await content.GetAllPagesAsync(ctx.RequestAborted), monitoring, filterDay, responder.BasePath, structure);

        // repeated small next to the title too, so clearing a day filter doesn't need a scroll first
        var headerFilterClear = filterDay is { } headerDay
            ? BuildHeaderFilterIndicator(l, headerDay, responder.BasePath)
            : "";

        var noIndexStatus = content.SiteConfig?.NoIndex?.Status ?? false;
        await responder.WriteAsync(ctx, new PageView(
            Title: l.StatusPageTitle,
            ContentHtml: $"<header class=\"page-header\"><h1 class=\"page-title\">{LayoutProvider.HtmlEncode(l.StatusPageTitle)}</h1>{headerFilterClear}</header>" + html,
            CanonicalPath: "",
            Prose: true,
            NoIndex: filterDay is not null || noIndexStatus));
    }

    private static DateOnly? ParseFilterDay(string? raw) =>
        DateOnly.TryParseExact(raw, "yyyy-MM-dd", out var day) ? day : null;

    private static string BuildHeaderFilterIndicator(Localization l, DateOnly day, string basePath) =>
        "<span class=\"status-filter-clear--header\"><span>"
        + LayoutProvider.HtmlEncode(l.StatusFilterShowing(DateFormatter.Current.Medium(day.ToDateTime(TimeOnly.MinValue))))
        + "</span><a href=\"" + basePath + "/#status-incidents\" class=\"status-filter-clear\">"
        + LayoutProvider.HtmlEncode(l.StatusFilterClear) + "</a></span>";

    private static string BuildStatusHtml(HeartbeatStore store, IReadOnlyList<MonitorTarget> targets, IReadOnlyList<DocumentationPage> pages, MonitoringConfig? monitoring, DateOnly? filterDay, string basePath, IWardenStructure structure)
    {
        var l = Localization.Current;
        var incidentMonitorIds = IncidentContent.ActiveIncidentMonitorIds(pages);
        var maintainedIds = IncidentContent.ActiveMaintenanceMonitorIds(pages, DateTimeOffset.UtcNow);
        var statuses = targets.ToDictionary(t => t.Id, t => IncidentContent.StatusOverride(t.Id, incidentMonitorIds, maintainedIds) ?? LatestStatus(store, t.Id));
        var anyDown = statuses.Values.Any(s => s is MonitorStatus.Down or MonitorStatus.Degraded);

        var recentIncidents = filterDay is null
            ? IncidentContent.RecentIncidents(pages, DateTimeOffset.UtcNow, monitoring?.IncidentWindowDays ?? IncidentContent.DefaultIncidentWindowDays, monitoring?.IncidentMaxShown ?? IncidentContent.DefaultIncidentMaxShown)
            : IncidentContent.IncidentsOnDay(pages, filterDay.Value, monitoring?.IncidentMaxShown ?? IncidentContent.DefaultIncidentMaxShown);

        var sb = new System.Text.StringBuilder();
        sb.Append("<p class=\"status-banner status-banner--")
          .Append(anyDown ? "down" : "up").Append(" select-none\">")
          .Append(LayoutProvider.HtmlEncode(anyDown ? l.StatusSomeDown : l.StatusAllOperational))
          .Append("</p>");

        if (structure.ShowStatusHeader)
        {
            AppendOverallUptime(sb, l, store, targets);
            // ongoing incidents always surface here, above the list/grid - readers shouldn't have to scroll past a green page to find out why something is down
            AppendOngoingIncidents(sb, l, recentIncidents, basePath);
        }

        AppendMonitors(sb, l, store, targets, statuses, basePath, monitoring?.Group, structure.UseCardStatusLayout, monitoring?.HistoryDays ?? HistoryDays);

        BuildIncidentsSection(sb, l, recentIncidents, basePath, filterDay);
        BuildMaintenanceSection(sb, l, pages, monitoring, filterDay, basePath);

        return sb.ToString();
    }

    // fflat list item ("clean", "default", and every structure that is not the card grid)
    private static string BuildFlatMonitorItem(Localization l, HeartbeatStore store, MonitorTarget target, MonitorStatus status, string basePath, int historyDays)
    {
        var uptime = store.GetUptime(target.Id, UptimeWindow);
        var sb = new System.Text.StringBuilder("<li class=\"status-monitor status-monitor--").Append(StatusClass(status)).Append("\">")
          .Append("<span class=\"status-monitor-name\">").Append(LayoutProvider.HtmlEncode(target.Name)).Append("</span>")
          .Append("<span class=\"status-monitor-badge select-none\">").Append(LayoutProvider.HtmlEncode(StatusLabel(l, status))).Append("</span>");
        if (uptime is { } u)
            sb.Append("<span class=\"status-monitor-uptime select-none\">").Append(LayoutProvider.HtmlEncode(l.StatusUptimeLabel(u.Percent, FormatDuration(u.Span)))).Append("</span>");
        sb.Append(BuildHistoryBar(store, target.Id, basePath, historyDays));
        sb.Append("</li>");
        return sb.ToString();
    }

    // status header (overall uptime + pinned ongoing incidents), on for "default" and "dashboard"
    internal static void AppendOverallUptime(System.Text.StringBuilder sb, Localization l, HeartbeatStore store, IReadOnlyList<MonitorTarget> targets)
    {
        var measured = targets
            .Select(t => store.GetUptime(t.Id, TimeSpan.FromDays(HistoryDays)))
            .Where(u => u is not null)
            .Select(u => u!.Value)
            .ToList();
        if (measured.Count == 0) return;

        // the window is however much history actually exists, not the 90 days we asked for - a fresh
        // deployment with a minute of heartbeats must not advertise "100% over the last 90 days"
        var span = measured.Max(u => u.Span);
        sb.Append("<p class=\"status-overall-uptime select-none\">")
          .Append(LayoutProvider.HtmlEncode(l.StatusOverallUptime(measured.Average(u => u.Percent), FormatDuration(span))))
          .Append("</p>");
    }

    private static void AppendOngoingIncidents(System.Text.StringBuilder sb, Localization l, List<DocumentationPage> recentIncidents, string basePath)
    {
        var ongoing = recentIncidents.Where(p => p.End is null).ToList();
        if (ongoing.Count == 0) return;

        sb.Append("<section class=\"status-ongoing-incidents\"><h2 class=\"status-group-heading select-none\">")
          .Append(LayoutProvider.HtmlEncode(l.StatusOngoingIncidentsHeading)).Append("</h2>");
        foreach (var page in ongoing)
            AppendIncidentArticle(sb, l, page, basePath);
        sb.Append("</section>");
    }

    private static string TypeLabel(MonitorTarget target) =>
        TypeGroupLabels.TryGetValue(target.Type, out var known) ? known : target.Type;

    // grouping is opt-in via monitoring.group and independent of the structure: "type" groups by each target's own
    // type, "custom" by its "group" field (falling back to the type label), unset renders one ungrouped section
    internal static void AppendMonitors(System.Text.StringBuilder sb, Localization l, HeartbeatStore store, IReadOnlyList<MonitorTarget> targets, Dictionary<string, MonitorStatus> statuses, string basePath, string? groupBy, bool cards, int historyDays = HistoryDays)
    {
        Func<MonitorTarget, string>? groupLabel = groupBy switch
        {
            "type" => TypeLabel,
            "custom" => t => t.Group ?? TypeLabel(t),
            _ => null,
        };

        if (groupLabel is null)
        {
            AppendMonitorSection(sb, l, store, targets, statuses, basePath, cards, heading: null, historyDays);
            return;
        }

        foreach (var group in targets.GroupBy(groupLabel, StringComparer.OrdinalIgnoreCase))
            AppendMonitorSection(sb, l, store, group, statuses, basePath, cards, group.Key, historyDays);
    }

    private static void AppendMonitorSection(System.Text.StringBuilder sb, Localization l, HeartbeatStore store, IEnumerable<MonitorTarget> targets, Dictionary<string, MonitorStatus> statuses, string basePath, bool cards, string? heading, int historyDays)
    {
        sb.Append("<section class=\"status-group\">");
        if (heading is not null)
            sb.Append("<h2 class=\"status-group-heading select-none\">").Append(LayoutProvider.HtmlEncode(heading)).Append("</h2>");
        sb.Append(cards ? "<ul class=\"status-monitor-grid\">" : "<ul class=\"status-monitor-list\">");
        foreach (var target in targets)
        {
            if (cards)
                AppendMonitorCard(sb, l, store, target, statuses[target.Id], basePath);
            else
                sb.Append(BuildFlatMonitorItem(l, store, target, statuses[target.Id], basePath, historyDays));
        }
        sb.Append("</ul></section>");
    }

    private static void AppendMonitorCard(System.Text.StringBuilder sb, Localization l, HeartbeatStore store, MonitorTarget target, MonitorStatus status, string basePath)
    {
        var uptime = store.GetUptime(target.Id, UptimeWindow);
        sb.Append("<li class=\"status-monitor-card status-monitor-card--").Append(StatusClass(status)).Append("\">")
          .Append("<div class=\"status-monitor-card-head\">")
          .Append("<span class=\"status-monitor-dot select-none\" aria-hidden=\"true\"></span>")
          .Append("<span class=\"status-monitor-name\">").Append(LayoutProvider.HtmlEncode(target.Name)).Append("</span>")
          .Append("<span class=\"status-monitor-badge select-none\">").Append(LayoutProvider.HtmlEncode(StatusLabel(l, status))).Append("</span>")
          .Append("</div>");
        if (uptime is { } u)
            sb.Append("<span class=\"status-monitor-uptime select-none\">").Append(LayoutProvider.HtmlEncode(l.StatusUptimeLabel(u.Percent, FormatDuration(u.Span)))).Append("</span>");
        sb.Append(BuildResponseTimeChart(store, target.Id, basePath));
        sb.Append(BuildHistoryBar(store, target.Id, basePath, ResponseChartDays));
        sb.Append("</li>");
    }

    // bar height scales to that monitor's busiest day; no successful checks yet renders no chart rather than a fake flat line; bars are real ?on=<day> links like the history ticks, so they're keyboard-focusable too
    private static string BuildResponseTimeChart(HeartbeatStore store, string monitorId, string basePath)
    {
        var l = Localization.Current;
        var days = store.GetResponseTimeHistory(monitorId, ResponseChartDays);
        var max = days.Where(d => d.AvgResponseMs is not null).Select(d => d.AvgResponseMs!.Value).DefaultIfEmpty(0).Max();
        if (max <= 0) return string.Empty;

        var sb = new System.Text.StringBuilder("<div class=\"status-response-chart\">");
        foreach (var day in days)
        {
            var scale = day.AvgResponseMs is { } ms ? Math.Max(0.04, ms / max) : 0;
            var tip = day.AvgResponseMs is { } avg ? $"{Math.Round(avg)} ms · {DateFormatter.Current.Medium(day.Day.ToDateTime(TimeOnly.MinValue))}" : l.StatusNoData;
            sb.Append("<a href=\"").Append(basePath).Append("/?on=").Append(day.Day.ToString("yyyy-MM-dd"))
              .Append("#status-incidents\" class=\"status-tick status-response-bar\" style=\"--bar-h:")
              .Append(scale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
              .Append("\" data-tip=\"").Append(LayoutProvider.HtmlEncode(tip)).Append("\"></a>");
        }
        sb.Append("</div>");
        return sb.ToString();
    }

    // hand-authored content/incidents/*.md; ongoing and recently-resolved both show, newest first
    private static void BuildIncidentsSection(System.Text.StringBuilder sb, Localization l, List<DocumentationPage> incidents, string basePath, DateOnly? filterDay)
    {
        sb.Append("<section class=\"status-incidents\" id=\"status-incidents\"><h2 class=\"status-group-heading select-none\">")
          .Append(LayoutProvider.HtmlEncode(l.StatusIncidentsHeading));
        if (filterDay is { } day)
            sb.Append(BuildHeaderFilterIndicator(l, day, basePath));
        sb.Append("</h2>");
        if (incidents.Count == 0)
        {
            sb.Append("<p class=\"status-no-incidents\">").Append(LayoutProvider.HtmlEncode(l.StatusNoIncidents)).Append("</p>");
            sb.Append("</section>");
            return;
        }

        foreach (var page in incidents)
            AppendIncidentArticle(sb, l, page, basePath);
        sb.Append("</section>");
    }

    // shared by the incidents list and the ongoing-incidents panel, so a pinned-to-top incident looks like its history entry, not a second alert box
    private static void AppendIncidentArticle(System.Text.StringBuilder sb, Localization l, DocumentationPage page, string basePath)
    {
        var resolved = page.End is not null;
        var badgeClass = IncidentContent.IncidentBadgeClass(page);
        var degraded = IncidentContent.IncidentStatus(page) == MonitorStatus.Degraded;
        var content = resolved
            ? l.StatusOutagePeriod(IncidentContent.TimeHtml(IncidentContent.StartOf(page)), IncidentContent.TimeHtml(IncidentContent.EndOf(page)!.Value))
            : degraded
                ? l.StatusDegradedSince(IncidentContent.TimeHtml(IncidentContent.StartOf(page)))
                : l.StatusDownSince(IncidentContent.TimeHtml(IncidentContent.StartOf(page)));
        sb.Append("<article class=\"status-incident\"><div class=\"status-incident-head\"><h3 class=\"status-incident-title\"><a href=\"")
          .Append(UrlPaths.Href(basePath, page.Path)).Append("\">")
          .Append(LayoutProvider.HtmlEncode(page.Title)).Append("</a></h3>")
          .Append("<span class=\"status-incident-badge status-incident-badge--").Append(badgeClass).Append("\">")
          .Append(LayoutProvider.HtmlEncode(resolved ? l.StatusResolved : degraded ? l.StatusDegraded : l.StatusDown)).Append("</span>")
          .Append("</div><div class=\"status-incident-content\">")
          .Append(content)
          .Append("</div></article>");
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

    internal static string FormatDuration(TimeSpan span) => span switch
    {
        { TotalDays: >= 1 } => $"{(int)Math.Round(span.TotalDays)}d",
        { TotalHours: >= 1 } => $"{(int)Math.Round(span.TotalHours)}h",
        // "min", never "m": a bare "m" next to "d"/"h" reads as months to half the people who see it
        _ => $"{Math.Max(1, (int)Math.Round(span.TotalMinutes))} min",
    };

    // one tick per calendar day, links to ?on=<day>; the dashboard card grid passes a shorter window than the flat list's HistoryDays, since 90 ticks reads as noise at card width
    private static string BuildHistoryBar(HeartbeatStore store, string monitorId, string basePath, int windowDays = HistoryDays)
    {
        var l = Localization.Current;
        var days = store.GetDailyStatus(monitorId, windowDays);
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
        MonitorStatus.Degraded => "degraded",
        _ => "unknown",
    };

    private static string StatusLabel(Localization l, MonitorStatus status) => status switch
    {
        MonitorStatus.Up => l.StatusOperational,
        MonitorStatus.Down => l.StatusDown,
        MonitorStatus.Maintenance => l.StatusMonitorMaintenance,
        MonitorStatus.Degraded => l.StatusDegraded,
        _ => l.StatusUnknown,
    };
}
