using Microsoft.Extensions.Logging.Abstractions;
using Warden.Configuration;
using Warden.Endpoints;
using Warden.Models;
using Warden.Services;
using Warden.Services.Rendering;

namespace Warden.Tests;

// grouping comes from monitoring.group only
public sealed class StatusGroupingTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"warden-test-{Guid.NewGuid():N}.db");
    private readonly HeartbeatStore _store;

    private static readonly List<MonitorTarget> Targets =
    [
        new("blog", "Blog", Url: "https://example.test", Group: "Publiek"),
        new("s3", "S3", Url: "https://s3.example.test", Type: "service_backend", Group: "Services"),
    ];

    public StatusGroupingTests() =>
        _store = new HeartbeatStore(new MonitoringOptions { DatabasePath = _dbPath }, NullLogger<HeartbeatStore>.Instance);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            if (File.Exists(path)) File.Delete(path);
    }

    private string Render(string? groupBy)
    {
        var sb = new System.Text.StringBuilder();
        var statuses = Targets.ToDictionary(t => t.Id, _ => MonitorStatus.Unknown);
        StatusEndpoints.AppendMonitors(sb, Localization.Current, _store, Targets, statuses, [], "", groupBy);
        return sb.ToString();
    }

    [Fact]
    public void CustomGroup_RendersSections()
    {
        var html = Render("custom");
        Assert.Contains("<h2 class=\"status-group-heading select-none\">Publiek</h2>", html);
        Assert.Contains("<h2 class=\"status-group-heading select-none\">Services</h2>", html);
    }

    [Fact]
    public void TypeGroup_RendersSections()
    {
        var html = Render("type");
        Assert.Contains(">HTTP</h2>", html);
        Assert.Contains(">Service health</h2>", html);
    }

    [Fact]
    public void NoGroup_RendersOneUngroupedSection()
    {
        var html = Render(null);
        Assert.DoesNotContain("status-group-heading", html);
        Assert.Equal(1, html.Split("<section class=\"status-group\">").Length - 1);
    }

    // a fresh deployment holds minutes of history; neither label may dress that up as 90 days
    [Fact]
    public void UptimeLabelsReportTheHistoryThatActuallyExists()
    {
        _store.Record("blog", DateTimeOffset.UtcNow, up: true, responseMs: 12);

        var flat = Render(null);
        Assert.Contains("100% uptime (1 min)", flat);
        Assert.DoesNotContain("uptime (1m)", flat);

        var sb = new System.Text.StringBuilder();
        StatusEndpoints.AppendOverallUptime(sb, Localization.Current, _store, Targets, TimeSpan.FromMinutes(1));
        Assert.Contains("Collecting uptime data, 1 min so far", sb.ToString());
        Assert.DoesNotContain("% uptime", sb.ToString());
        Assert.DoesNotContain("90 days", sb.ToString());
    }

    [Fact]
    public void OverallUptime_ShowsPercent_OnceADayOfHistoryExists()
    {
        _store.Record("blog", DateTimeOffset.UtcNow.AddDays(-2), up: true, responseMs: 12);
        _store.Record("blog", DateTimeOffset.UtcNow, up: true, responseMs: 12);

        // daily interval: gap is not missing
        var sb = new System.Text.StringBuilder();
        StatusEndpoints.AppendOverallUptime(sb, Localization.Current, _store, Targets, TimeSpan.FromDays(1));
        Assert.Contains("% uptime over the last", sb.ToString());
    }

    // per-monitor uptime spans the history bar, not 24h
    [Fact]
    public void MonitorUptime_CoversTheHistoryWindow()
    {
        _store.Record("blog", DateTimeOffset.UtcNow.AddDays(-3), up: true, responseMs: 12);
        _store.Record("blog", DateTimeOffset.UtcNow, up: true, responseMs: 12);

        var sb = new System.Text.StringBuilder();
        var statuses = Targets.ToDictionary(t => t.Id, _ => MonitorStatus.Up);
        StatusEndpoints.AppendMonitors(sb, Localization.Current, _store, Targets, statuses, [], "", null, historyDays: 30, expectedInterval: TimeSpan.FromDays(2));
        Assert.Contains("100% uptime (3d)", sb.ToString());
    }

    [Fact]
    public void Banner_MarksLiveViewForRefresh_OnlyWhenGivenAnInterval()
    {
        var live = new System.Text.StringBuilder();
        StatusEndpoints.AppendBanner(live, Localization.Current, [("Blog", MonitorStatus.Up)], null, refreshSeconds: 300);
        Assert.Contains("data-refresh=\"300\"", live.ToString());
        Assert.Contains("aria-live=\"polite\"", live.ToString());

        var past = new System.Text.StringBuilder();
        StatusEndpoints.AppendBanner(past, Localization.Current, [("Blog", MonitorStatus.Up)], null);
        Assert.DoesNotContain("data-refresh", past.ToString());
    }

    [Theory]
    [InlineData(MonitorStatus.Up, MonitorStatus.Up, "status-banner--up", "All systems operational")]
    [InlineData(MonitorStatus.Degraded, MonitorStatus.Up, "status-banner--degraded", "Degraded: Blog")]
    [InlineData(MonitorStatus.Degraded, MonitorStatus.Down, "status-banner--down", "Down: S3")]
    [InlineData(MonitorStatus.Maintenance, MonitorStatus.Maintenance, "status-banner--maintenance", "Maintenance: Blog, S3")]
    public void Banner_NamesTheMonitorsInTheWorstState(MonitorStatus blog, MonitorStatus s3, string tone, string title)
    {
        var sb = new System.Text.StringBuilder();
        StatusEndpoints.AppendBanner(sb, Localization.Current, [("Blog", blog), ("S3", s3)], lastChecked: null);
        var html = sb.ToString();
        Assert.Contains(tone, html);
        Assert.Contains(">" + title + "</h1>", html);
        Assert.DoesNotContain("status-banner-meta", html);
    }

    // a monitor with zero heartbeats must say so plainly, not silently drop the uptime line
    [Fact]
    public void UptimeLine_ShowsNoData_WhenMonitorHasNoHeartbeats()
    {
        var html = Render(null);
        Assert.Contains(Localization.Current.StatusNoData, html);
    }

    // filtering to a past day must show that day's uptime%, not the live 24h figure - and every group must get
    // it, not just the first one the header-indicator logic renders it next to
    [Fact]
    public void FilteredDay_ShowsThatDaysUptime_ForEveryGroup()
    {
        var day = DateOnly.FromDateTime(DateTimeOffset.UtcNow.AddDays(-2).UtcDateTime);
        var dayStart = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        _store.Record("blog", dayStart.AddHours(1), up: true, responseMs: 10);
        _store.Record("blog", dayStart.AddHours(2), up: false, responseMs: null);
        _store.Record("s3", dayStart.AddHours(1), up: true, responseMs: 10);
        _store.Record("s3", dayStart.AddHours(2), up: true, responseMs: 10);

        var sb = new System.Text.StringBuilder();
        var statuses = Targets.ToDictionary(t => t.Id, _ => MonitorStatus.Down);
        StatusEndpoints.AppendMonitors(sb, Localization.Current, _store, Targets, statuses, [], "", "custom", filterDay: day);

        var html = sb.ToString();
        Assert.Contains("50% uptime (1d)", html);
        Assert.Contains("100% uptime (1d)", html);
        Assert.DoesNotContain("(24h)", html);
    }

    // degraded never borrows the Down badge
    [Fact]
    public void DegradedRendersItsOwnBadge()
    {
        var sb = new System.Text.StringBuilder();
        var statuses = Targets.ToDictionary(t => t.Id, t => t.Id == "s3" ? MonitorStatus.Degraded : MonitorStatus.Up);
        StatusEndpoints.AppendMonitors(sb, Localization.Current, _store, Targets, statuses, [], "", null);

        var html = sb.ToString();
        Assert.Contains("--degraded", html);
        Assert.Contains(">Degraded<", html);
    }
}
