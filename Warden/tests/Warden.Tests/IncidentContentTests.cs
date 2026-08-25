using Warden.Models;
using Warden.Services;

namespace Warden.Tests;

public sealed class IncidentContentTests
{
    private static DocumentationPage Page(string relativePath, DateTime start, bool maintenance = false, DateTime? end = null, IReadOnlyList<string>? monitors = null, string? status = null) =>
        new("incidents/x", "Title", "<p>body</p>", OriginalRelativePath: relativePath, Date: start, Maintenance: maintenance, End: end, Monitors: monitors, Status: status);

    [Fact]
    public void RecentIncidents_IncludesOngoingRegardlessOfWindowButAgesOutResolved()
    {
        var now = DateTimeOffset.UtcNow;
        var pages = new[]
        {
            Page("incidents/old-ongoing.md", now.AddDays(-30).UtcDateTime),
            Page("incidents/recent-resolved.md", now.AddDays(-2).UtcDateTime, end: now.AddDays(-1).UtcDateTime),
            Page("incidents/stale-resolved.md", now.AddDays(-30).UtcDateTime, end: now.AddDays(-29).UtcDateTime),
            Page("incidents/window.md", now.AddHours(-1).UtcDateTime, maintenance: true, end: now.AddHours(1).UtcDateTime),
            Page("pages/unrelated.md", now.UtcDateTime),
        };

        var recent = IncidentContent.RecentIncidents(pages, now, windowDays: 7, maxShown: 10);

        Assert.Equal(["incidents/recent-resolved.md", "incidents/old-ongoing.md"], recent.Select(p => p.OriginalRelativePath));
    }

    [Fact]
    public void RecentIncidents_OrdersMostRecentFirstAndCapsCount()
    {
        var now = DateTimeOffset.UtcNow;
        var pages = new[]
        {
            Page("incidents/older.md", now.AddDays(-2).UtcDateTime),
            Page("incidents/newer.md", now.AddHours(-1).UtcDateTime),
        };

        var recent = IncidentContent.RecentIncidents(pages, now, windowDays: 7, maxShown: 1);

        Assert.Equal(["incidents/newer.md"], recent.Select(p => p.OriginalRelativePath));
    }

    [Fact]
    public void IncidentBadgeClass_ReflectsResolution()
    {
        var now = DateTime.UtcNow;
        Assert.Equal("down", IncidentContent.IncidentBadgeClass(Page("incidents/a.md", now)));
        Assert.Equal("resolved", IncidentContent.IncidentBadgeClass(Page("incidents/a.md", now, end: now)));
    }

    [Fact]
    public void MaintenanceBadgeClass_ReflectsPlannedActiveEnded()
    {
        var now = DateTimeOffset.UtcNow;
        var planned = Page("incidents/a.md", now.AddDays(1).UtcDateTime, maintenance: true, end: now.AddDays(2).UtcDateTime);
        var active = Page("incidents/a.md", now.AddHours(-1).UtcDateTime, maintenance: true, end: now.AddHours(1).UtcDateTime);
        var ended = Page("incidents/a.md", now.AddDays(-2).UtcDateTime, maintenance: true, end: now.AddDays(-1).UtcDateTime);

        Assert.Equal("planned", IncidentContent.MaintenanceBadgeClass(planned, now));
        Assert.Equal("active", IncidentContent.MaintenanceBadgeClass(active, now));
        Assert.Equal("ended", IncidentContent.MaintenanceBadgeClass(ended, now));
    }

    [Fact]
    public void UpcomingMaintenance_DropsPastAndFarFutureWindowsAndIncidents()
    {
        var now = DateTimeOffset.UtcNow;
        var pages = new[]
        {
            Page("incidents/past.md", now.AddDays(-2).UtcDateTime, maintenance: true, end: now.AddDays(-1).UtcDateTime),
            Page("incidents/planned.md", now.AddDays(1).UtcDateTime, maintenance: true, end: now.AddDays(2).UtcDateTime),
            Page("incidents/far-future.md", now.AddDays(30).UtcDateTime, maintenance: true, end: now.AddDays(31).UtcDateTime),
            Page("incidents/an-incident.md", now.AddHours(-1).UtcDateTime),
        };

        var upcoming = IncidentContent.UpcomingMaintenance(pages, now, windowDays: 14, maxShown: 10);

        Assert.Single(upcoming);
        Assert.Equal("incidents/planned.md", upcoming[0].OriginalRelativePath);
    }

    [Fact]
    public void IncidentsOnDay_FindsIncidentOutsideDefaultWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var farBack = now.AddDays(-40);
        var pages = new[]
        {
            Page("incidents/old.md", farBack.UtcDateTime, end: farBack.AddHours(1).UtcDateTime),
            Page("incidents/unrelated-day.md", now.AddDays(-1).UtcDateTime),
        };

        var found = IncidentContent.IncidentsOnDay(pages, DateOnly.FromDateTime(farBack.UtcDateTime), maxShown: 10);

        Assert.Single(found);
        Assert.Equal("incidents/old.md", found[0].OriginalRelativePath);
    }

    [Fact]
    public void MaintenanceOnDay_FindsWindowOutsideDefaultRange()
    {
        var now = DateTimeOffset.UtcNow;
        var farFuture = now.AddDays(60);
        var pages = new[]
        {
            Page("incidents/far.md", farFuture.UtcDateTime, maintenance: true, end: farFuture.AddHours(2).UtcDateTime),
            Page("incidents/soon.md", now.AddDays(1).UtcDateTime, maintenance: true, end: now.AddDays(1).AddHours(2).UtcDateTime),
        };

        var found = IncidentContent.MaintenanceOnDay(pages, DateOnly.FromDateTime(farFuture.UtcDateTime), maxShown: 10);

        Assert.Single(found);
        Assert.Equal("incidents/far.md", found[0].OriginalRelativePath);
    }

    [Fact]
    public void ActiveMaintenanceMonitorIds_OnlyCountsActiveWindowsWithMonitorsListed()
    {
        var now = DateTimeOffset.UtcNow;
        var pages = new[]
        {
            Page("incidents/active-linked.md", now.AddHours(-1).UtcDateTime, maintenance: true, end: now.AddHours(1).UtcDateTime, monitors: ["forgejo", "codeberg"]),
            Page("incidents/active-unlinked.md", now.AddHours(-1).UtcDateTime, maintenance: true, end: now.AddHours(1).UtcDateTime),
            Page("incidents/planned-linked.md", now.AddDays(1).UtcDateTime, maintenance: true, end: now.AddDays(2).UtcDateTime, monitors: ["fedora-magazine"]),
        };

        var ids = IncidentContent.ActiveMaintenanceMonitorIds(pages, now, ["forgejo", "codeberg", "fedora-magazine"]);

        Assert.Equal(new HashSet<string> { "forgejo", "codeberg" }, ids);
    }

    [Fact]
    public void ActiveIncidentMonitorIds_OnlyCountsUnresolvedIncidentsWithMonitorsListed()
    {
        var now = DateTime.UtcNow;
        var pages = new[]
        {
            Page("incidents/ongoing-linked.md", now.AddHours(-1), monitors: ["forgejo"]),
            Page("incidents/ongoing-unlinked.md", now.AddHours(-1)),
            Page("incidents/resolved-linked.md", now.AddHours(-2), end: now.AddHours(-1), monitors: ["codeberg"]),
        };

        var ids = IncidentContent.ActiveIncidentMonitorIds(pages, ["forgejo", "codeberg"]);

        Assert.Equal(new Dictionary<string, MonitorStatus> { ["forgejo"] = MonitorStatus.Down }, ids);
    }

    [Fact]
    public void ActiveIncidentMonitorIds_ExpandsAllToEveryConfiguredMonitor()
    {
        var now = DateTime.UtcNow;
        var pages = new[] { Page("incidents/system-wide.md", now.AddHours(-1), monitors: ["all"]) };

        var ids = IncidentContent.ActiveIncidentMonitorIds(pages, ["forgejo", "codeberg", "blog"]);

        Assert.Equal(new Dictionary<string, MonitorStatus>
        {
            ["forgejo"] = MonitorStatus.Down,
            ["codeberg"] = MonitorStatus.Down,
            ["blog"] = MonitorStatus.Down,
        }, ids);
    }

    // "API responses degraded" is not an outage: the probe keeps passing, so the badge must say so instead of claiming Down
    [Fact]
    public void ActiveIncidentMonitorIds_HonoursDegradedStatusAndKeepsTheHarsherOneOnOverlap()
    {
        var now = DateTime.UtcNow;
        var pages = new[]
        {
            Page("incidents/slow.md", now.AddHours(-1), monitors: ["forgejo"], status: "Degraded"),
            Page("incidents/typo.md", now.AddHours(-1), monitors: ["codeberg"], status: "not-a-status"),
            Page("incidents/both-a.md", now.AddHours(-2), monitors: ["blog"], status: "degraded"),
            Page("incidents/both-b.md", now.AddHours(-1), monitors: ["blog"]),
        };

        var ids = IncidentContent.ActiveIncidentMonitorIds(pages, ["forgejo", "codeberg", "blog"]);

        Assert.Equal(MonitorStatus.Degraded, ids["forgejo"]);
        Assert.Equal(MonitorStatus.Down, ids["codeberg"]);
        Assert.Equal(MonitorStatus.Down, ids["blog"]);
    }

    [Fact]
    public void StatusOverride_IncidentBeatsMaintenanceBeatsNull()
    {
        var incidentIds = new Dictionary<string, MonitorStatus> { ["forgejo"] = MonitorStatus.Down, ["blog"] = MonitorStatus.Degraded };
        var maintenanceIds = new HashSet<string> { "forgejo", "blog", "codeberg" };

        Assert.Equal(MonitorStatus.Down, IncidentContent.StatusOverride("forgejo", incidentIds, maintenanceIds));
        Assert.Equal(MonitorStatus.Degraded, IncidentContent.StatusOverride("blog", incidentIds, maintenanceIds));
        Assert.Equal(MonitorStatus.Maintenance, IncidentContent.StatusOverride("codeberg", incidentIds, maintenanceIds));
        Assert.Null(IncidentContent.StatusOverride("fedora-magazine", incidentIds, maintenanceIds));
    }
}
