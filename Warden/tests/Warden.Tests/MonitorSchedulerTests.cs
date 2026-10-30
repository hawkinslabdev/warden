using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Warden.Configuration;
using Warden.Models;
using Warden.Services;

namespace Warden.Tests;

public sealed class MonitorSchedulerTests
{
    [Theory]
    [InlineData(null, null, 90)]   // defaults: 30 retention, 90 bar -> keep 90
    [InlineData(1, null, 90)]      // retention shorter than bar -> bar wins
    [InlineData(0, 7, 7)]          // 0 is not "forever"; still keeps what is drawn
    [InlineData(120, null, 120)]   // longer retention untouched
    [InlineData(1, 1000, 365)]     // history clamps to 365
    public void RetentionDays_NeverBelowDisplayedHistory(int? retention, int? history, int expected)
    {
        var config = new MonitoringConfig(null, null, retention, null, null, null, null, history);
        Assert.Equal(expected, MonitorScheduler.RetentionDays(config));
    }

    [Theory]
    [InlineData("""{"status":"ok"}""", "$.status", "ok")]
    [InlineData("""{"data":{"db":"up"}}""", "$.data.db", "up")]
    [InlineData("""{"pools":[{"name":"main","ok":true},{"name":"replica","ok":true}]}""", "$.pools[?@.ok==false]", null)]
    [InlineData("""{"pools":["down","up","up"]}""", "$.pools[0]", "down")]
    public void ExtractJsonValue_SupportsFullJsonPathSyntax(string json, string path, string? expected)
    {
        var node = JsonNode.Parse(json);
        Assert.Equal(expected, MonitorScheduler.ExtractJsonValue(node, path));
    }

    [Fact]
    public void ExtractJsonValue_MissingSegmentReturnsNull()
    {
        var node = JsonNode.Parse("""{"status":"ok"}""");
        Assert.Null(MonitorScheduler.ExtractJsonValue(node, "$.missing.path"));
    }

    [Fact]
    public void EvaluateCertExpiry_UpWhenWellBeforeWarningWindow()
    {
        var now = new DateTime(2026, 1, 1);
        var (up, error) = MonitorScheduler.EvaluateCertExpiry(now.AddDays(60), warnDaysBefore: 30, now);
        Assert.True(up);
        Assert.Null(error);
    }

    [Fact]
    public void EvaluateCertExpiry_DownWithinWarningWindow()
    {
        var now = new DateTime(2026, 1, 1);
        var (up, error) = MonitorScheduler.EvaluateCertExpiry(now.AddDays(10), warnDaysBefore: 30, now);
        Assert.False(up);
        Assert.Contains("expires in", error);
    }

    [Fact]
    public void EvaluateCertExpiry_DownWhenAlreadyExpired()
    {
        var now = new DateTime(2026, 1, 1);
        var (up, error) = MonitorScheduler.EvaluateCertExpiry(now.AddDays(-1), warnDaysBefore: 30, now);
        Assert.False(up);
        Assert.Contains("expired", error);
    }

    // proof-of-fix: many webhook URLs (Slack/Discord/Teams) carry their auth secret in the path or query.
    // logging the raw URL on failure would leak it (OWASP A09/A02); SafeHost must strip everything but scheme+host+port.
    [Theory]
    [InlineData("https://hooks.slack.com/services/T00/B00/xoxb-super-secret-token", "https://hooks.slack.com")]
    [InlineData("https://example.com:8443/hook?token=super-secret-token", "https://example.com:8443")]
    [InlineData("https://user:pass@example.com/hook", "https://example.com")]
    public void SafeHost_NeverLeaksPathQueryOrCredentials(string webhookUrl, string expectedLoggedValue)
    {
        var safe = MonitorScheduler.SafeHost(webhookUrl);
        Assert.Equal(expectedLoggedValue, safe);
        Assert.DoesNotContain("secret", safe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", safe, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pass", safe, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SafeHost_InvalidUrl_DoesNotThrowOrEcho()
    {
        Assert.Equal("(invalid url)", MonitorScheduler.SafeHost("not a url"));
    }
}

// full cycles over fake network
public sealed class MonitorSchedulerTransitionTests : IDisposable
{
    private const string Site = "https://site.test/";
    private const string Uplink = "https://uplink.test/";
    private const string Hook = "https://hook.test/";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"warden-test-{Guid.NewGuid():N}.db");
    private readonly HeartbeatStore _store;
    private readonly FakeNetwork _net = new();
    private readonly MonitorScheduler _scheduler;

    public MonitorSchedulerTransitionTests()
    {
        _store = new HeartbeatStore(new MonitoringOptions { DatabasePath = _dbPath }, NullLogger<HeartbeatStore>.Instance);
        var content = new ContentService(new DocsOptions { RootPath = Path.GetTempPath(), EnableHotReload = false }, new MarkdownService(), NullLogger<ContentService>.Instance);
        _scheduler = new MonitorScheduler(_net, _store, content, NullLogger<MonitorScheduler>.Instance);
        _net.Up.Add(Uplink);
        _net.Up.Add(Hook);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            if (File.Exists(path)) File.Delete(path);
    }

    private static MonitoringConfig Config(bool uplinkProbe = false, int? cooldown = null) =>
        new(null, null, null, null, null, null, null, null,
            Webhooks: [new WebhookTarget(Hook)],
            WebhookCooldownMinutes: cooldown,
            ConnectivityUrls: uplinkProbe ? [Uplink] : null);

    private Task Cycle(MonitoringConfig config, int? retries = null) =>
        _scheduler.CheckAllAsync([new MonitorTarget("site", "Site", Site, Retries: retries)], config, CancellationToken.None);

    private bool? LatestUp => _store.GetLatest("site")?.Data.Up;

    [Fact]
    public async Task UplinkDown_FailuresNotRecordedOrAnnounced()
    {
        var config = Config(uplinkProbe: true);
        _net.Up.Clear(); // provider outage

        await Cycle(config);
        await Cycle(config);

        Assert.Null(LatestUp);
        Assert.Empty(_net.Webhooks);
    }

    [Fact]
    public async Task UplinkUp_TargetDown_RecordedAndAnnounced()
    {
        var config = Config(uplinkProbe: true);

        await Cycle(config);

        Assert.False(LatestUp);
        Assert.Equal(["down"], _net.Webhooks);
    }

    [Fact]
    public async Task NoProbeConfigured_KeepsOldBehavior()
    {
        _net.Up.Remove(Uplink);
        await Cycle(Config());
        Assert.False(LatestUp);
    }

    [Fact]
    public async Task DownWebhookUndelivered_RetriedUntilDelivered_ThenRecovery()
    {
        var config = Config();
        _net.Up.Remove(Hook);
        await Cycle(config); // receiver unreachable
        await Cycle(config);
        _net.Up.Add(Hook);
        await Cycle(config); // receiver back, still down
        await Cycle(config);
        _net.Up.Add(Site);
        await Cycle(config);

        Assert.Equal(["down", "up"], _net.Webhooks);
    }

    [Fact]
    public async Task DownNeverDelivered_NoLoneRecovery()
    {
        var config = Config();
        _net.Up.Remove(Hook);
        await Cycle(config);
        _net.Up.Add(Hook);
        _net.Up.Add(Site);
        await Cycle(config);

        Assert.Empty(_net.Webhooks);
    }

    [Fact]
    public async Task RetriesLoweredMidOutage_StillAnnouncesDown()
    {
        var config = Config();
        await Cycle(config, retries: 3); // pending 1/3
        await Cycle(config, retries: 3); // pending 2/3
        await Cycle(config, retries: 1); // 3 > 1: down
        _net.Up.Add(Site);
        await Cycle(config, retries: 1);

        Assert.Equal(["down", "up"], _net.Webhooks);
    }

    [Fact]
    public async Task RetriesRaisedMidOutage_RecoveryStillSent_NextOutageAnnounced()
    {
        var config = Config();
        await Cycle(config);              // down announced
        await Cycle(config, retries: 5);  // pending by count
        _net.Up.Add(Site);
        await Cycle(config, retries: 5);
        _net.Up.Remove(Site);
        await Cycle(config);

        Assert.Equal(["down", "up", "down"], _net.Webhooks);
    }

    [Fact]
    public async Task Flapping_WithinCooldown_NoRepeatAlerts()
    {
        var config = Config(cooldown: 60);
        await Cycle(config);
        _net.Up.Add(Site);
        await Cycle(config);
        _net.Up.Remove(Site);
        await Cycle(config); // held back
        _net.Up.Add(Site);
        await Cycle(config); // unannounced: no recovery

        Assert.Equal(["down", "up"], _net.Webhooks);
    }

    private sealed class FakeNetwork : HttpMessageHandler, IHttpClientFactory
    {
        public HashSet<string> Up { get; } = [];
        public List<string> Webhooks { get; } = [];

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            if (!Up.Contains(url))
                throw new HttpRequestException("unreachable");
            if (url == Hook)
            {
                var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                Webhooks.Add((string)body!["status"]!);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
