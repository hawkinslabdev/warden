using Warden.Models;

namespace Warden.Services;

// checks every target from content/config.json's "monitoring" block on an interval; reads config fresh each cycle, no restart needed to edit targets
public sealed class MonitorScheduler(
    IHttpClientFactory httpClientFactory,
    HeartbeatStore store,
    ContentService content,
    ILogger<MonitorScheduler> logger) : BackgroundService
{
    public const string HttpClientName = "monitor-check";
    private const int DefaultIntervalSeconds = 60;
    private const int DefaultRetentionDays = 30;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var monitoring = content.SiteConfig?.Monitoring;
            var targets = monitoring?.Targets ?? [];

            if (targets.Count == 0)
                logger.LogDebug("No monitoring targets configured; nothing to check this cycle");
            else
            {
                await CheckAllAsync(targets, stoppingToken);
                store.PruneOlderThan(TimeSpan.FromDays(Math.Max(1, monitoring?.RetentionDays ?? DefaultRetentionDays)));
            }

            var interval = TimeSpan.FromSeconds(Math.Max(5, monitoring?.IntervalSeconds ?? DefaultIntervalSeconds));
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private async Task CheckAllAsync(IReadOnlyList<MonitorTarget> targets, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        foreach (var target in targets)
        {
            var timestamp = DateTimeOffset.UtcNow;
            var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                using var response = await client.GetAsync(target.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var elapsedMs = (int)System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                store.Record(target.Id, timestamp, response.IsSuccessStatusCode, elapsedMs);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                var elapsedMs = (int)System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                store.Record(target.Id, timestamp, up: false, elapsedMs, ex.Message);
                logger.LogWarning("Check failed for {MonitorId} ({Url}): {Message}", target.Id, target.Url, ex.Message);
            }
        }
    }
}
