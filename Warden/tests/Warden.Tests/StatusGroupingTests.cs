using Microsoft.Extensions.Logging.Abstractions;
using Warden.Configuration;
using Warden.Endpoints;
using Warden.Models;
using Warden.Services;
using Warden.Services.Rendering;

namespace Warden.Tests;

// grouping comes from monitoring.group alone: every structure honours it, and no structure invents groups without it
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

    private string Render(string? groupBy, bool cards)
    {
        var sb = new System.Text.StringBuilder();
        var statuses = Targets.ToDictionary(t => t.Id, _ => MonitorStatus.Unknown);
        StatusEndpoints.AppendMonitors(sb, Localization.Current, _store, Targets, statuses, [], "", groupBy, cards);
        return sb.ToString();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CustomGroup_SectionsBothLayouts(bool cards)
    {
        var html = Render("custom", cards);
        Assert.Contains("<h2 class=\"status-group-heading select-none\">Publiek</h2>", html);
        Assert.Contains("<h2 class=\"status-group-heading select-none\">Services</h2>", html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypeGroup_SectionsBothLayouts(bool cards)
    {
        var html = Render("type", cards);
        Assert.Contains(">HTTP</h2>", html);
        Assert.Contains(">Service health</h2>", html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NoGroup_RendersOneUngroupedSection(bool cards)
    {
        var html = Render(null, cards);
        Assert.DoesNotContain("status-group-heading", html);
        Assert.Equal(1, html.Split("<section class=\"status-group\">").Length - 1);
    }

    // a fresh deployment holds minutes of history; neither label may dress that up as 90 days
    [Fact]
    public void UptimeLabelsReportTheHistoryThatActuallyExists()
    {
        _store.Record("blog", DateTimeOffset.UtcNow, up: true, responseMs: 12);

        var flat = Render(null, cards: false);
        Assert.Contains("100% uptime (1 min)", flat);
        Assert.DoesNotContain("uptime (1m)", flat);

        var sb = new System.Text.StringBuilder();
        StatusEndpoints.AppendOverallUptime(sb, Localization.Current, _store, Targets);
        Assert.Contains("100% uptime over the last 1 min", sb.ToString());
        Assert.DoesNotContain("90 days", sb.ToString());
    }

    // a degraded monitor must not borrow the Down badge, in either layout
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DegradedRendersItsOwnBadge(bool cards)
    {
        var sb = new System.Text.StringBuilder();
        var statuses = Targets.ToDictionary(t => t.Id, t => t.Id == "s3" ? MonitorStatus.Degraded : MonitorStatus.Up);
        StatusEndpoints.AppendMonitors(sb, Localization.Current, _store, Targets, statuses, [], "", null, cards);

        var html = sb.ToString();
        Assert.Contains("--degraded", html);
        Assert.Contains(">Degraded<", html);
    }

    [Fact]
    public void LayoutFollowsStructureIndependentlyOfGrouping()
    {
        Assert.Contains("status-monitor-list", Render("custom", cards: false));
        Assert.Contains("status-monitor-grid", Render("custom", cards: true));
    }
}
