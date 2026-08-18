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

    // one bucket per UTC calendar day, oldest first, for a left-to-right history bar; a day with any down heartbeat is "down", any data at all with no downtime is "up", no rows that day is "unknown"
    public List<DailyStatus> GetDailyStatus(string monitorId, int days)
    {
        var sinceDay = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(days - 1));

        using var connection = Open();
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT timestamp, data FROM heartbeats WHERE monitor_id = $monitorId AND timestamp >= $since ORDER BY timestamp ASC;";
        select.Parameters.AddWithValue("$monitorId", monitorId);
        select.Parameters.AddWithValue("$since", sinceDay.ToDateTime(TimeOnly.MinValue).ToString("O"));

        var daysWithData = new HashSet<DateOnly>();
        var daysWithDowntime = new HashSet<DateOnly>();
        using var reader = select.ExecuteReader();
        while (reader.Read())
        {
            var day = DateOnly.FromDateTime(DateTimeOffset.Parse(reader.GetString(0)).UtcDateTime);
            daysWithData.Add(day);
            if (!Deserialize(reader.GetString(1)).Up)
                daysWithDowntime.Add(day);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = new List<DailyStatus>();
        for (var day = sinceDay; day <= today; day = day.AddDays(1))
        {
            var status = !daysWithData.Contains(day) ? MonitorStatus.Unknown
                : daysWithDowntime.Contains(day) ? MonitorStatus.Down
                : MonitorStatus.Up;
            result.Add(new DailyStatus(day, status));
        }
        return result;
    }

    // share of "up" heartbeats within window, plus how much of the window is actually covered by data (a fresh target has minutes of history, not the full window); null when there's no data yet
    public (double Percent, TimeSpan Span)? GetUptime(string monitorId, TimeSpan window)
    {
        using var connection = Open();
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT timestamp, data FROM heartbeats WHERE monitor_id = $monitorId AND timestamp >= $since ORDER BY timestamp ASC;";
        select.Parameters.AddWithValue("$monitorId", monitorId);
        select.Parameters.AddWithValue("$since", (DateTimeOffset.UtcNow - window).ToString("O"));

        var total = 0;
        var up = 0;
        DateTimeOffset? earliest = null;
        using var reader = select.ExecuteReader();
        while (reader.Read())
        {
            earliest ??= DateTimeOffset.Parse(reader.GetString(0));
            total++;
            if (Deserialize(reader.GetString(1)).Up)
                up++;
        }
        if (total == 0 || earliest is null)
            return null;

        var span = DateTimeOffset.UtcNow - earliest.Value;
        if (span > window) span = window;
        return (100.0 * up / total, span);
    }

    // ponytail: fixed retention window; make it a per-monitor setting if that's ever needed.
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
