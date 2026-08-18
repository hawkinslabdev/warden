using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.Json;
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
    private const int CheckTimeoutSeconds = 10;
    private const int MaxConcurrentChecks = 8;

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
                    var (up, error) = await CheckOneAsync(target, client, timeoutCts.Token);
                    var elapsedMs = (int)System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                    store.Record(target.Id, timestamp, up, elapsedMs, error);
                    if (!up)
                        logger.LogWarning("Check failed for {MonitorId} ({Type}): {Message}", target.Id, target.Type, error);
                }
                catch (Exception ex) when (ex is HttpRequestException or SocketException or IOException or PingException
                    || (ex is OperationCanceledException && !ct.IsCancellationRequested))
                {
                    var elapsedMs = (int)System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                    var message = ex is OperationCanceledException ? "timed out" : ex.Message;
                    store.Record(target.Id, timestamp, up: false, elapsedMs, message);
                    logger.LogWarning("Check failed for {MonitorId} ({Type}): {Message}", target.Id, target.Type, message);
                }
            });
    }

    private static Task<(bool Up, string? Error)> CheckOneAsync(MonitorTarget target, HttpClient client, CancellationToken ct) =>
        target.Type switch
        {
            "service_backend" => CheckServiceBackendAsync(target, client, ct),
            "ping" => RequireHost(target, host => CheckPingAsync(host, ct)),
            "tcp" or "database" => RequireHostAndPort(target, (host, port) => CheckTcpAsync(host, port, ct)),
            "ftp" => RequireHost(target, host => CheckFtpAsync(target, host, ct)),
            "sftp" => RequireHost(target, host => CheckSftpAsync(host, target.Port ?? 22, ct)),
            "dns" => RequireHost(target, host => CheckDnsAsync(target, host, ct)),
            "ssl" => RequireHost(target, host => CheckSslAsync(target, host, ct)),
            _ => CheckHttpAsync(target, client, ct),
        };

    // config validation as data, not exceptions - throwing here would escape Parallel.ForEachAsync's catch filter below
    private static Task<(bool, string?)> RequireHost(MonitorTarget target, Func<string, Task<(bool, string?)>> check) =>
        target.Host is { } host ? check(host) : Task.FromResult<(bool, string?)>((false, $"target '{target.Id}' is missing 'host'"));

    private static Task<(bool, string?)> RequireHostAndPort(MonitorTarget target, Func<string, int, Task<(bool, string?)>> check) =>
        target.Host is not { } host ? Task.FromResult<(bool, string?)>((false, $"target '{target.Id}' is missing 'host'"))
        : target.Port is not { } port ? Task.FromResult<(bool, string?)>((false, $"target '{target.Id}' is missing 'port'"))
        : check(host, port);

    private static async Task<(bool, string?)> CheckHttpAsync(MonitorTarget target, HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(target.Url, HttpCompletionOption.ResponseHeadersRead, ct);
        return response.IsSuccessStatusCode ? (true, null) : (false, $"HTTP {(int)response.StatusCode}");
    }

    private static async Task<(bool, string?)> CheckServiceBackendAsync(MonitorTarget target, HttpClient client, CancellationToken ct)
    {
        using var response = await client.GetAsync(target.Url, HttpCompletionOption.ResponseContentRead, ct);
        var expectedStatus = target.ExpectedStatus ?? 200;
        if ((int)response.StatusCode != expectedStatus)
            return (false, $"HTTP {(int)response.StatusCode}, expected {expectedStatus}");

        if (target.ExpectedJsonPath is null)
            return (true, null);

        await using var body = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(body, cancellationToken: ct);
        var actual = ExtractJsonValue(doc.RootElement, target.ExpectedJsonPath);
        if (actual is null)
            return (false, $"JSON path '{target.ExpectedJsonPath}' not found");
        if (target.ExpectedValue is not null && actual != target.ExpectedValue)
            return (false, $"'{target.ExpectedJsonPath}' was '{actual}', expected '{target.ExpectedValue}'");
        return (true, null);
    }

    // supports plain dot paths ("$.status" / "status.nested"); no array indices or filters
    // ponytail: hand-rolled, swap for a JSONPath package if that's ever needed
    internal static string? ExtractJsonValue(JsonElement root, string path)
    {
        var current = root;
        foreach (var segment in path.TrimStart('$', '.').Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
                return null;
            current = next;
        }
        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => current.GetRawText(),
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

    private const byte SshMsgKexInit = 20;
    private const int SshMaxPacketLength = 35_000; // RFC 4253 §6.1 cap

    // verifies the server follows its banner with a well-formed SSH_MSG_KEXINIT packet (RFC 4253 §7.1) - proof of a real SSH transport, not a banner-echoing decoy
    // ponytail: stops short of the Diffie-Hellman exchange + host-key verification; add SSH.NET if a decoy that fakes one valid KEXINIT packet is ever a real threat
    private static async Task<(bool, string?)> CheckSftpAsync(string host, int port, CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, ct);
        using var stream = client.GetStream();

        var banner = await ReadLineAsync(stream, ct);
        if (!banner.StartsWith("SSH-", StringComparison.Ordinal))
            return (false, $"unexpected banner: {banner}");

        var messageCode = await ReadSshMessageCodeAsync(stream, ct);
        return messageCode == SshMsgKexInit
            ? (true, null)
            : (false, $"expected SSH_MSG_KEXINIT (20) after banner, got message {messageCode}");
    }

    // binary packet framing per RFC 4253 §6; we only need the payload's first byte (the message code)
    private static async Task<byte> ReadSshMessageCodeAsync(NetworkStream stream, CancellationToken ct)
    {
        var header = new byte[4];
        await ReadExactAsync(stream, header, ct);
        var packetLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header);
        if (packetLength is 0 or > SshMaxPacketLength)
            throw new IOException($"implausible SSH packet length {packetLength}");

        var messageCode = new byte[1];
        await ReadExactAsync(stream, messageCode, ct);
        return messageCode[0];
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), ct);
            if (read == 0)
                throw new IOException("connection closed before expected data arrived");
            offset += read;
        }
    }

    private static async Task<(bool, string?)> CheckDnsAsync(MonitorTarget target, string host, CancellationToken ct)
    {
        var addresses = await System.Net.Dns.GetHostAddressesAsync(host, ct);
        if (addresses.Length == 0)
            return (false, "no addresses returned");
        if (target.ExpectedIp is null)
            return (true, null);
        return addresses.Any(a => a.ToString() == target.ExpectedIp)
            ? (true, null)
            : (false, $"resolved to [{string.Join(", ", addresses.Select(a => a.ToString()))}], expected {target.ExpectedIp}");
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
