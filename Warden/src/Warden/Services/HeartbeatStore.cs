using System.Text.Json;
using Microsoft.Data.Sqlite;
using Warden.Configuration;
using Warden.Models;
using Warden.Serialization;

namespace Warden.Services;

// self-collected uptime history in a local sqlite file; rows carry a short random id, the check result is one json "data" column
public sealed class HeartbeatStore
{
    private readonly string _connectionString;
    private readonly ILogger<HeartbeatStore> _logger;

    public HeartbeatStore(MonitoringOptions options, ILogger<HeartbeatStore> logger)
    {
        _logger = logger;
        var dbPath = Path.IsPathRooted(options.DatabasePath)
            ? options.DatabasePath
            : Path.Combine(AppContext.BaseDirectory, options.DatabasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();

        using var connection = Open();
        using (var pragma = connection.CreateCommand())
        {
            // WAL: writes never block a reader
            pragma.CommandText = "PRAGMA journal_mode=WAL;";
            pragma.ExecuteNonQuery();
        }
        EnsureIncrementalVacuum(connection);
        using var create = connection.CreateCommand();
        create.CommandText = """
            -- timestamp is always UTC (DateTimeOffset.UtcNow), independent of the TZ env var
            CREATE TABLE IF NOT EXISTS heartbeats (
                id TEXT PRIMARY KEY,
                monitor_id TEXT NOT NULL,
                timestamp TEXT NOT NULL,
                data TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_heartbeats_monitor_time ON heartbeats(monitor_id, timestamp);
            """;
        create.ExecuteNonQuery();
    }

    // auto_vacuum only takes effect on an empty database, so migrating an existing file needs a one-time VACUUM; idempotent since later startups see mode already INCREMENTAL
    private static void EnsureIncrementalVacuum(SqliteConnection connection)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "PRAGMA auto_vacuum;";
        var mode = Convert.ToInt32(check.ExecuteScalar());
        if (mode == 2) // already INCREMENTAL
            return;

        using var setMode = connection.CreateCommand();
        setMode.CommandText = "PRAGMA auto_vacuum = INCREMENTAL;";
        setMode.ExecuteNonQuery();

        using var vacuum = connection.CreateCommand();
        vacuum.CommandText = "VACUUM;";
        vacuum.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        // Record() can now be called from several threads at once, so let SQLite wait out a busy writer instead of throwing SQLITE_BUSY immediately
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..12];

    public void Record(string monitorId, DateTimeOffset timestamp, bool up, int? responseMs, string? error = null)
    {
        var payload = JsonSerializer.Serialize(new HeartbeatPayload(up, responseMs, error), WardenJsonContext.Default.HeartbeatPayload);

        using var connection = Open();
        using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO heartbeats (id, monitor_id, timestamp, data) VALUES ($id, $monitorId, $timestamp, $data);";
        insert.Parameters.AddWithValue("$id", NewId());
        insert.Parameters.AddWithValue("$monitorId", monitorId);
        insert.Parameters.AddWithValue("$timestamp", timestamp.ToString("O"));
        insert.Parameters.AddWithValue("$data", payload);
        insert.ExecuteNonQuery();
    }

    public HeartbeatRecord? GetLatest(string monitorId)
    {
        using var connection = Open();
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT id, timestamp, data FROM heartbeats WHERE monitor_id = $monitorId ORDER BY timestamp DESC LIMIT 1;";
        select.Parameters.AddWithValue("$monitorId", monitorId);
        using var reader = select.ExecuteReader();
        return reader.Read() ? ReadRecord(reader, monitorId) : null;
    }

    public List<DailyStatus> GetDailyStatus(string monitorId, int days, double? degradedBelowPercent = null)
    {
        var sinceDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(days - 1));

        using var connection = Open();
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT timestamp, data FROM heartbeats WHERE monitor_id = $monitorId AND timestamp >= $since ORDER BY timestamp ASC;";
        select.Parameters.AddWithValue("$monitorId", monitorId);
        select.Parameters.AddWithValue("$since", sinceDay.ToDateTime(TimeOnly.MinValue).ToString("O"));

        var counts = new Dictionary<DateOnly, (int Total, int Down)>();
        using var reader = select.ExecuteReader();
        while (reader.Read())
        {
            var day = DateOnly.FromDateTime(DateTimeOffset.Parse(reader.GetString(0)).UtcDateTime);
            var (total, down) = counts.GetValueOrDefault(day);
            counts[day] = (total + 1, down + (Deserialize(reader.GetString(1)).Up ? 0 : 1));
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = new List<DailyStatus>();
        for (var day = sinceDay; day <= today; day = day.AddDays(1))
        {
            if (!counts.TryGetValue(day, out var c))
            {
                result.Add(new DailyStatus(day, MonitorStatus.Unknown));
                continue;
            }

            var upPercent = 100.0 * (c.Total - c.Down) / c.Total;
            var isDown = degradedBelowPercent is { } threshold ? upPercent < threshold : c.Down == c.Total;
            var status = c.Down == 0 ? MonitorStatus.Up : isDown ? MonitorStatus.Down : MonitorStatus.Degraded;
            result.Add(new DailyStatus(day, status, upPercent));
        }
        return result;
    }

    // one bucket per UTC calendar day, average of that day's recorded response times (down/errored checks carry no responseMs and don't count); for the per-monitor latency chart
    public List<DailyResponseTime> GetResponseTimeHistory(string monitorId, int days)
    {
        var sinceDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(days - 1));

        using var connection = Open();
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT timestamp, data FROM heartbeats WHERE monitor_id = $monitorId AND timestamp >= $since ORDER BY timestamp ASC;";
        select.Parameters.AddWithValue("$monitorId", monitorId);
        select.Parameters.AddWithValue("$since", sinceDay.ToDateTime(TimeOnly.MinValue).ToString("O"));

        var sumByDay = new Dictionary<DateOnly, (long Sum, int Count)>();
        using var reader = select.ExecuteReader();
        while (reader.Read())
        {
            var day = DateOnly.FromDateTime(DateTimeOffset.Parse(reader.GetString(0)).UtcDateTime);
            if (Deserialize(reader.GetString(1)).ResponseMs is not { } ms)
                continue;
            var (sum, count) = sumByDay.GetValueOrDefault(day);
            sumByDay[day] = (sum + ms, count + 1);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = new List<DailyResponseTime>();
        for (var day = sinceDay; day <= today; day = day.AddDays(1))
        {
            var avg = sumByDay.TryGetValue(day, out var bucket) ? (double)bucket.Sum / bucket.Count : (double?)null;
            result.Add(new DailyResponseTime(day, avg));
        }
        return result;
    }

    private const int GapMultiplier = 3;

    public (double Percent, TimeSpan Span)? GetUptime(string monitorId, TimeSpan window, TimeSpan? expectedInterval = null)
    {
        using var connection = Open();
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT timestamp, data FROM heartbeats WHERE monitor_id = $monitorId AND timestamp >= $since ORDER BY timestamp ASC;";
        select.Parameters.AddWithValue("$monitorId", monitorId);
        select.Parameters.AddWithValue("$since", (DateTimeOffset.UtcNow - window).ToString("O"));

        var beats = new List<(DateTimeOffset Timestamp, bool Up)>();
        using var reader = select.ExecuteReader();
        while (reader.Read())
            beats.Add((DateTimeOffset.Parse(reader.GetString(0)), Deserialize(reader.GetString(1)).Up));
        if (beats.Count == 0)
            return null;

        var interval = expectedInterval ?? TimeSpan.FromSeconds(MonitorScheduler.DefaultIntervalSeconds);
        var gapThreshold = interval * GapMultiplier;
        var now = DateTimeOffset.UtcNow;

        var upSpan = TimeSpan.Zero;
        var totalSpan = TimeSpan.Zero;
        for (var i = 0; i < beats.Count; i++)
        {
            var gap = (i + 1 < beats.Count ? beats[i + 1].Timestamp : now) - beats[i].Timestamp;
            var measuredGap = gap > gapThreshold ? interval : gap;
            totalSpan += measuredGap;
            if (beats[i].Up)
                upSpan += measuredGap;
        }

        if (totalSpan <= TimeSpan.Zero)
            return (beats[^1].Up ? 100.0 : 0.0, TimeSpan.Zero);

        var span = totalSpan > window ? window : totalSpan;
        return (100.0 * upSpan.TotalSeconds / totalSpan.TotalSeconds, span);
    }

    // one retention window for all monitors, set via config.json's "retentionDays" or MonitorScheduler's default - intentionally not per-monitor
    public void PruneOlderThan(TimeSpan retention)
    {
        using var connection = Open();
        using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM heartbeats WHERE timestamp < $cutoff;";
        delete.Parameters.AddWithValue("$cutoff", (DateTimeOffset.UtcNow - retention).ToString("O"));
        var removed = delete.ExecuteNonQuery();
        if (removed > 0)
        {
            _logger.LogDebug("Pruned {Count} heartbeats older than {Retention}", removed, retention);
            // reclaims the pages the delete just freed, without a full VACUUM's whole-file rewrite
            using var incrementalVacuum = connection.CreateCommand();
            incrementalVacuum.CommandText = "PRAGMA incremental_vacuum;";
            incrementalVacuum.ExecuteNonQuery();
        }
    }

    private static HeartbeatRecord ReadRecord(SqliteDataReader reader, string monitorId) =>
        new(reader.GetString(0), monitorId, DateTimeOffset.Parse(reader.GetString(1)), Deserialize(reader.GetString(2)));

    private static HeartbeatPayload Deserialize(string json) =>
        JsonSerializer.Deserialize(json, WardenJsonContext.Default.HeartbeatPayload)!;
}
