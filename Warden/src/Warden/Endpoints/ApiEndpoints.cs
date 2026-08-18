using Microsoft.AspNetCore.Http.HttpResults;
using Warden.Configuration;
using Warden.Models;
using Warden.Services;

namespace Warden.Endpoints;

internal static class ApiEndpoints
{
    private static readonly TimeSpan UptimeWindow = TimeSpan.FromHours(24);

    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");
        api.MapMethods("/pages", HttpVerbs.GetAndHead, GetPages).RequireRateLimiting(RateLimitPolicies.Api);
        api.MapMethods("/status", HttpVerbs.GetAndHead, GetStatus).RequireRateLimiting(RateLimitPolicies.Api);
        // NOT rate-limited; the hot-reload script polls this every few seconds
        api.MapMethods("/build-version", HttpVerbs.GetAndHead, GetBuildVersion);
        return app;
    }

    // public page metadata only; no OriginalRelativePath or other server file paths
    internal static async Task<Ok<List<PageSummary>>> GetPages(ContentService docs, CancellationToken cancellationToken)
    {
        var pages = await docs.GetAllPagesAsync(cancellationToken);
        var items = pages
            .OrderBy(p => p.Path)
            .Select(p => new PageSummary(p.Path, p.Title, p.Description, p.LastModified))
            .ToList();
        return TypedResults.Ok(items);
    }

    internal static async Task<Ok<StatusApiResponse>> GetStatus(HeartbeatStore store, ContentService content, CancellationToken cancellationToken)
    {
        var monitoring = content.SiteConfig?.Monitoring;
        var targets = monitoring?.Targets ?? [];
        var pages = await content.GetAllPagesAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var incidentMonitorIds = IncidentContent.ActiveIncidentMonitorIds(pages);
        var maintainedIds = IncidentContent.ActiveMaintenanceMonitorIds(pages, now);
        var monitors = targets.Select(t =>
        {
            var latest = store.GetLatest(t.Id);
            var overridden = IncidentContent.StatusOverride(t.Id, incidentMonitorIds, maintainedIds);
            var status = overridden switch
            {
                MonitorStatus.Down => "down",
                MonitorStatus.Maintenance => "maintenance",
                _ => latest is null ? "unknown" : latest.Data.Up ? "up" : "down",
            };
            var uptime = store.GetUptime(t.Id, UptimeWindow);
            return new ApiMonitorStatus(t.Id, t.Name, status, uptime?.Percent, latest?.Timestamp);
        }).ToList();

        var incidents = IncidentContent
            .RecentIncidents(pages, now, monitoring?.IncidentWindowDays ?? IncidentContent.DefaultIncidentWindowDays, monitoring?.IncidentMaxShown ?? IncidentContent.DefaultIncidentMaxShown)
            .Select(p => new ApiIncident(p.Path, p.Title, p.Description, IncidentContent.StartOf(p), IncidentContent.EndOf(p), IncidentContent.IncidentBadgeClass(p)))
            .ToList();

        var maintenance = IncidentContent
            .UpcomingMaintenance(pages, now, monitoring?.MaintenanceWindowDays ?? IncidentContent.DefaultMaintenanceWindowDays, monitoring?.MaintenanceMaxShown ?? IncidentContent.DefaultMaintenanceMaxShown)
            .Select(p => new ApiMaintenanceWindow(p.Path, p.Title, IncidentContent.StartOf(p), IncidentContent.EndOf(p)!.Value, p.Description, IncidentContent.MaintenanceBadgeClass(p, now)))
            .ToList();

        return TypedResults.Ok(new StatusApiResponse(monitors, incidents, maintenance));
    }

    internal static Ok<BuildVersionResponse> GetBuildVersion(HttpContext context, ContentService docs)
    {
        // "no-store" not just "no-cache"; the hot-reload poll needs the live value every time.
        context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        context.Response.Headers.Pragma = "no-cache";
        return TypedResults.Ok(new BuildVersionResponse(docs.BuildVersion));
    }
}
