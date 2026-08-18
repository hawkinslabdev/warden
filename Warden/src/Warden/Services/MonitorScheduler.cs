using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using DnsClient;
using Json.Path;
using Renci.SshNet;
using Renci.SshNet.Common;
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
    public const string InsecureHttpClientName = "monitor-check-insecure";
    private const int DefaultIntervalSeconds = 60;
    private const int DefaultRetentionDays = 30;
    private const int CheckTimeoutSeconds = 10;
    private const int MaxConcurrentChecks = 8;

    // in-memory only (resets on restart); a failure short of target.Retries is pending, not recorded, so one blip doesn't flip the public status
    private readonly ConcurrentDictionary<string, int> _consecutiveFailures = new();

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
                try
                {
                    await CheckAllAsync(targets, stoppingToken);
                    store.PruneOlderThan(TimeSpan.FromDays(Math.Max(1, monitoring?.RetentionDays ?? DefaultRetentionDays)));
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    // one bad cycle must not end the loop, or a single check bug silently kills monitoring for every target until the next deploy
                    logger.LogError(ex, "Monitor check cycle failed unexpectedly");
                }
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

    // targets are independent I/O-bound probes with a per-check timeout, so they run with bounded concurrency instead of one-at-a-time
    private async Task CheckAllAsync(IReadOnlyList<MonitorTarget> targets, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);
        var insecureClient = httpClientFactory.CreateClient(InsecureHttpClientName);
        await Parallel.ForEachAsync(
            targets,
            new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentChecks, CancellationToken = cancellationToken },
            async (target, ct) =>
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(CheckTimeoutSeconds));

                var timestamp = DateTimeOffset.UtcNow;
                var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    var (up, error) = await CheckOneAsync(target, target.Insecure == true ? insecureClient : client, timeoutCts.Token);
                    var elapsedMs = (int)System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                    if (up)
                        RecordUp(target, timestamp, elapsedMs);
                    else
                        RecordDown(target, timestamp, elapsedMs, error);
                }
                catch (Exception ex) when (ex is HttpRequestException or SocketException or IOException or PingException
                    || (ex is OperationCanceledException && !ct.IsCancellationRequested))
                {
                    var elapsedMs = (int)System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                    var message = ex is OperationCanceledException ? "timed out" : ex.Message;
                    RecordDown(target, timestamp, elapsedMs, message);
                }
            });
    }

    // routine "up" stays at Debug (too noisy for `docker compose logs` by default); anything worth watching - a retry, a real down, a recovery - logs at Information
    private void RecordUp(MonitorTarget target, DateTimeOffset timestamp, int elapsedMs)
    {
        var wasDown = _consecutiveFailures.TryRemove(target.Id, out var failures) && failures > (target.Retries ?? 0);
        store.Record(target.Id, timestamp, up: true, elapsedMs);
        if (wasDown)
            logger.LogInformation("[{MonitorId}] recovered ({Type}, {ElapsedMs}ms)", target.Id, target.Type, elapsedMs);
        else
            logger.LogDebug("[{MonitorId}] up ({Type}, {ElapsedMs}ms)", target.Id, target.Type, elapsedMs);
    }

    // a failure within target.Retries is "pending": logged but not recorded; Retries unset/0 keeps the original behavior - down on the very first failed check
    private void RecordDown(MonitorTarget target, DateTimeOffset timestamp, int elapsedMs, string? message)
    {
        var threshold = target.Retries ?? 0;
        var failures = _consecutiveFailures.AddOrUpdate(target.Id, 1, (_, n) => n + 1);
        if (failures <= threshold)
        {
            logger.LogInformation("[{MonitorId}] pending ({Type}): retry {Failures}/{Threshold} - {Message}", target.Id, target.Type, failures, threshold, message);
            return;
        }
        store.Record(target.Id, timestamp, up: false, elapsedMs, message);
        logger.LogWarning("[{MonitorId}] down ({Type}): {Message}", target.Id, target.Type, message);
    }

    private static Task<(bool Up, string? Error)> CheckOneAsync(MonitorTarget target, HttpClient client, CancellationToken ct) =>
        target.Type switch
        {
            "http" => RequireUrl(target, url => CheckHttpAsync(url, client, ct)),
            "service_backend" => RequireUrl(target, url => CheckServiceBackendAsync(target, url, client, ct)),
            "ping" => RequireHost(target, host => CheckPingAsync(host, ct)),
            "tcp" or "database" => RequireHostAndPort(target, (host, port) => CheckTcpAsync(host, port, ct)),
            "ftp" => RequireHost(target, host => CheckFtpAsync(target, host, ct)),
            "sftp" => RequireHost(target, host => CheckSftpAsync(host, target.Port ?? 22, ct)),
            "dns" => RequireHost(target, host => CheckDnsAsync(target, host, ct)),
            "ssl" => RequireHost(target, host => CheckSslAsync(target, host, ct)),
            // an unrecognized type must not silently fall back to an http check with no url to call - that's the crash this guards against
            _ => Task.FromResult<(bool, string?)>((false, $"target '{target.Id}' has unknown type '{target.Type}'")),
        };

    // config validation as data, not exceptions - throwing here would escape Parallel.ForEachAsync's catch filter below
    private static Task<(bool, string?)> RequireHost(MonitorTarget target, Func<string, Task<(bool, string?)>> check) =>
        target.Host is { } host ? check(host) : Task.FromResult<(bool, string?)>((false, $"target '{target.Id}' is missing 'host'"));

    private static Task<(bool, string?)> RequireHostAndPort(MonitorTarget target, Func<string, int, Task<(bool, string?)>> check) =>
        target.Host is not { } host ? Task.FromResult<(bool, string?)>((false, $"target '{target.Id}' is missing 'host'"))
        : target.Port is not { } port ? Task.FromResult<(bool, string?)>((false, $"target '{target.Id}' is missing 'port'"))
        : check(host, port);

    private static Task<(bool, string?)> RequireUrl(MonitorTarget target, Func<string, Task<(bool, string?)>> check) =>
        target.Url is { } url ? check(url) : Task.FromResult<(bool, string?)>((false, $"target '{target.Id}' is missing 'url'"));

    private static async Task<(bool, string?)> CheckHttpAsync(string url, HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        return response.IsSuccessStatusCode ? (true, null) : (false, $"HTTP {(int)response.StatusCode}");
    }

    private static async Task<(bool, string?)> CheckServiceBackendAsync(MonitorTarget target, string url, HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct);
        var expectedStatus = target.ExpectedStatus ?? 200;
        if ((int)response.StatusCode != expectedStatus)
            return (false, $"HTTP {(int)response.StatusCode}, expected {expectedStatus}");

        if (target.ExpectedJsonPath is null)
            return (true, null);

        await using var body = await response.Content.ReadAsStreamAsync(ct);
        var node = await JsonNode.ParseAsync(body, cancellationToken: ct);
        var actual = ExtractJsonValue(node, target.ExpectedJsonPath);
        if (actual is null)
            return (false, $"JSON path '{target.ExpectedJsonPath}' not found");
        if (target.ExpectedValue is not null && actual != target.ExpectedValue)
            return (false, $"'{target.ExpectedJsonPath}' was '{actual}', expected '{target.ExpectedValue}'");
        return (true, null);
    }

    // full RFC 9535 JSONPath (arrays, wildcards, filters); paths must start with "$" per spec
    internal static string? ExtractJsonValue(JsonNode? root, string path)
    {
        var match = JsonPath.Parse(path).Evaluate(root).Matches.FirstOrDefault()?.Value;
        return match switch
        {
            null => null,
            JsonValue v when v.TryGetValue<string>(out var s) => s,
            JsonValue v when v.TryGetValue<bool>(out var b) => b ? "true" : "false",
            _ => match.ToJsonString(),
        };
    }

    private static async Task<(bool, string?)> CheckPingAsync(string host, CancellationToken ct)
    {
        using var ping = new Ping();
        var reply = await ping.SendPingAsync(host, TimeSpan.FromSeconds(CheckTimeoutSeconds), cancellationToken: ct);
        return reply.Status == IPStatus.Success ? (true, null) : (false, reply.Status.ToString());
    }

    private static async Task<(bool, string?)> CheckTcpAsync(string host, int port, CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, ct);
        return (true, null);
    }

    private static async Task<(bool, string?)> CheckFtpAsync(MonitorTarget target, string host, CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, target.Port ?? 21, ct);
        using var stream = client.GetStream();
        var banner = await ReadLineAsync(stream, ct);
        if (!banner.StartsWith("220", StringComparison.Ordinal))
            return (false, $"unexpected banner: {banner}");

        if (target.Secure != true)
            return (true, null);

        await WriteLineAsync(stream, "AUTH TLS", ct);
        var authReply = await ReadLineAsync(stream, ct);
        if (!authReply.StartsWith("234", StringComparison.Ordinal))
            return (false, $"AUTH TLS refused: {authReply}");

        using var ssl = new SslStream(stream, leaveInnerStreamOpen: true);
        await ssl.AuthenticateAsClientAsync(host);
        return (true, null);
    }

    // connects with throwaway credentials to force a full key exchange before the server can even reject the password; SshAuthenticationException only fires after that handshake succeeds
    private static async Task<(bool, string?)> CheckSftpAsync(string host, int port, CancellationToken ct)
    {
        using var client = new SshClient(host, port, "warden-monitor", Guid.NewGuid().ToString("N"));
        try
        {
            await client.ConnectAsync(ct);
            return (true, null);
        }
        catch (SshAuthenticationException)
        {
            return (true, null);
        }
        catch (SshConnectionException ex)
        {
            return (false, ex.Message);
        }
    }

    private static async Task<(bool, string?)> CheckDnsAsync(MonitorTarget target, string host, CancellationToken ct)
    {
        var addresses = await ResolveAsync(target, host, ct);
        if (addresses.Count == 0)
            return (false, "no addresses returned");
        if (target.ExpectedIp is null)
            return (true, null);
        return addresses.Contains(target.ExpectedIp)
            ? (true, null)
            : (false, $"resolved to [{string.Join(", ", addresses)}], expected {target.ExpectedIp}");
    }

    // the OS resolver covers the common case; DnsClient only gets pulled in when DnsServer/Family are actually set
    private static async Task<List<string>> ResolveAsync(MonitorTarget target, string host, CancellationToken ct)
    {
        if (target.DnsServer is null && target.Family is null)
            return [.. (await System.Net.Dns.GetHostAddressesAsync(host, ct)).Select(a => a.ToString())];

        var lookup = target.DnsServer is { } server ? new LookupClient(System.Net.IPAddress.Parse(server), 53) : new LookupClient();
        var queryTypes = target.Family?.ToLowerInvariant() switch
        {
            "ipv6" => (QueryType[])[QueryType.AAAA],
            "ipv4" => [QueryType.A],
            _ => [QueryType.A, QueryType.AAAA],
        };

        var addresses = new List<string>();
        foreach (var queryType in queryTypes)
        {
            var result = await lookup.QueryAsync(host, queryType, cancellationToken: ct);
            addresses.AddRange(result.Answers
                .Select(record => record switch
                {
                    DnsClient.Protocol.ARecord a => a.Address.ToString(),
                    DnsClient.Protocol.AaaaRecord aaaa => aaaa.Address.ToString(),
                    _ => null,
                })
                .Where(a => a is not null)!);
        }
        return addresses;
    }

    private static async Task<(bool, string?)> CheckSslAsync(MonitorTarget target, string host, CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, target.Port ?? 443, ct);
        using var ssl = new SslStream(client.GetStream());
        await ssl.AuthenticateAsClientAsync(host);
        var raw = ssl.RemoteCertificate ?? throw new InvalidOperationException("server presented no certificate");
        using var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(raw);
        return EvaluateCertExpiry(cert.NotAfter, target.WarnDaysBefore ?? 14, DateTime.Now);
    }

    // pure so it's testable without a real TLS handshake
    internal static (bool, string?) EvaluateCertExpiry(DateTime expires, int warnDaysBefore, DateTime now)
    {
        var daysRemaining = (expires - now).TotalDays;
        if (daysRemaining < 0)
            return (false, $"certificate expired {expires:yyyy-MM-dd}");
        if (daysRemaining <= warnDaysBefore)
            return (false, $"certificate expires in {(int)daysRemaining} day(s) ({expires:yyyy-MM-dd})");
        return (true, null);
    }

    // reads exactly one CRLF-terminated line and no further - a bulk read here risks swallowing the start of whatever binary data follows (e.g. the SFTP check's KEXINIT packet)
    private static async Task<string> ReadLineAsync(NetworkStream stream, CancellationToken ct)
    {
        var line = new List<byte>();
        var one = new byte[1];
        while (await stream.ReadAsync(one, ct) != 0 && one[0] != '\n')
        {
            if (one[0] != '\r')
                line.Add(one[0]);
        }
        return System.Text.Encoding.ASCII.GetString(line.ToArray());
    }

    private static Task WriteLineAsync(NetworkStream stream, string line, CancellationToken ct) =>
        stream.WriteAsync(System.Text.Encoding.ASCII.GetBytes(line + "\r\n"), ct).AsTask();
}
