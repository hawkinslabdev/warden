using Microsoft.Extensions.Logging.Abstractions;
using Warden.Configuration;
using Warden.Models;
using Warden.Services;

namespace Warden.Tests;

// exercises the sqlite-backed heartbeat store end to end, no external monitoring backend involved
public sealed class HeartbeatStoreTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"warden-test-{Guid.NewGuid():N}.db");
    private readonly HeartbeatStore _store;

    public HeartbeatStoreTests()
    {
        _store = new HeartbeatStore(new MonitoringOptions { DatabasePath = _dbPath }, NullLogger<HeartbeatStore>.Instance);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            if (File.Exists(path)) File.Delete(path);
    }

    [Fact]
    public void Construction_EnablesIncrementalAutoVacuum()
    {
        Assert.Equal(2, ReadAutoVacuumMode(_dbPath));
    }

    [Fact]
    public void Construction_MigratesLegacyDatabaseWithoutLosingData()
    {
        var legacyDbPath = Path.Combine(Path.GetTempPath(), $"warden-test-{Guid.NewGuid():N}.db");
        try
        {
            using (var seed = new Microsoft.Data.Sqlite.SqliteConnection(
                new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = legacyDbPath }.ToString()))
            {
                seed.Open();
                using var create = seed.CreateCommand();
                create.CommandText = """
                    CREATE TABLE heartbeats (id TEXT PRIMARY KEY, monitor_id TEXT NOT NULL, timestamp TEXT NOT NULL, data TEXT NOT NULL);
                    INSERT INTO heartbeats VALUES ('abc123', 'legacy-site', '2026-01-01T00:00:00+00:00', '{"up":true,"responseMs":42,"error":null}');
                    """;
                create.ExecuteNonQuery();
            }
            Assert.Equal(0, ReadAutoVacuumMode(legacyDbPath)); // legacy default, before HeartbeatStore ever touches it

            var migrated = new HeartbeatStore(new MonitoringOptions { DatabasePath = legacyDbPath }, NullLogger<HeartbeatStore>.Instance);

            Assert.Equal(2, ReadAutoVacuumMode(legacyDbPath));
            var latest = migrated.GetLatest("legacy-site");
            Assert.NotNull(latest);
            Assert.True(latest!.Data.Up);
            Assert.Equal(42, latest.Data.ResponseMs);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var path in new[] { legacyDbPath, legacyDbPath + "-wal", legacyDbPath + "-shm" })
                if (File.Exists(path)) File.Delete(path);
        }
    }

    private static int ReadAutoVacuumMode(string dbPath)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath }.ToString());
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA auto_vacuum;";
        return Convert.ToInt32(pragma.ExecuteScalar());
    }

    [Fact]
    public void GetLatest_ReturnsMostRecentHeartbeat()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Record("site", now.AddMinutes(-1), up: true, responseMs: 50);
        _store.Record("site", now, up: false, responseMs: null, error: "timeout");

        var latest = _store.GetLatest("site");

        Assert.NotNull(latest);
        Assert.False(latest!.Data.Up);
        Assert.Equal("timeout", latest.Data.Error);
        Assert.Matches("^[0-9a-f]{12}$", latest.Id);
    }

    [Fact]
    public void GetUptime_ComputesShareOfUpHeartbeats()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Record("site", now.AddMinutes(-3), up: true, responseMs: 10);
        _store.Record("site", now.AddMinutes(-2), up: true, responseMs: 10);
        _store.Record("site", now.AddMinutes(-1), up: false, responseMs: null);
        _store.Record("site", now, up: true, responseMs: 10);

        var uptime = _store.GetUptime("site", TimeSpan.FromHours(24));

        Assert.Equal(75.0, uptime!.Value.Percent);
    }

    [Fact]
    public void GetUptime_ReportsActualSpan_WhenLessThanWindow()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Record("site", now.AddMinutes(-10), up: true, responseMs: 10);
        _store.Record("site", now, up: true, responseMs: 10);

        var uptime = _store.GetUptime("site", TimeSpan.FromHours(24));

        Assert.True(uptime!.Value.Span <= TimeSpan.FromMinutes(10.1));
        Assert.True(uptime.Value.Span >= TimeSpan.FromMinutes(9.9));
    }

    [Fact]
    public void GetUptime_ReturnsNull_WhenNoData() =>
        Assert.Null(_store.GetUptime("nothing-recorded", TimeSpan.FromHours(24)));

    [Fact]
    public void GetDailyStatus_MarksTodayDegraded_WhenSomeButNotAllHeartbeatsWereDown()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Record("site", now.AddHours(-2), up: true, responseMs: 10);
        _store.Record("site", now.AddHours(-1), up: false, responseMs: null);

        var days = _store.GetDailyStatus("site", 3);

        Assert.Equal(3, days.Count);
        Assert.Equal(MonitorStatus.Degraded, days[^1].Status);
        Assert.Equal(DateOnly.FromDateTime(now.UtcDateTime), days[^1].Day);
    }

    [Fact]
    public void GetDailyStatus_MarksTodayDown_WhenEveryHeartbeatWasDown()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Record("site", now.AddHours(-2), up: false, responseMs: null);
        _store.Record("site", now.AddHours(-1), up: false, responseMs: null);

        var days = _store.GetDailyStatus("site", 3);

        Assert.Equal(MonitorStatus.Down, days[^1].Status);
    }

    [Fact]
    public void GetDailyStatus_MarksDayUnknown_WhenNoData()
    {
        var days = _store.GetDailyStatus("nothing-recorded", 5);

        Assert.Equal(5, days.Count);
        Assert.All(days, d => Assert.Equal(MonitorStatus.Unknown, d.Status));
    }

    [Fact]
    public void GetDailyStatus_MarksDayUp_WhenOnlyUpHeartbeats()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Record("site", now, up: true, responseMs: 10);

        var days = _store.GetDailyStatus("site", 1);

        Assert.Equal(MonitorStatus.Up, Assert.Single(days).Status);
    }

    [Fact]
    public void GetResponseTimeHistory_AveragesSameDayHeartbeats_ExcludingDownChecks()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Record("site", now.AddHours(-2), up: true, responseMs: 100);
        _store.Record("site", now.AddHours(-1), up: true, responseMs: 200);
        _store.Record("site", now, up: false, responseMs: null);

        var history = _store.GetResponseTimeHistory("site", 1);

        Assert.Equal(150.0, Assert.Single(history).AvgResponseMs);
    }

    [Fact]
    public void GetResponseTimeHistory_NullForDaysWithNoData() =>
        Assert.All(_store.GetResponseTimeHistory("nothing-recorded", 3), d => Assert.Null(d.AvgResponseMs));

    [Fact]
    public void PruneOlderThan_RemovesOnlyStaleRows()
    {
        var now = DateTimeOffset.UtcNow;
        _store.Record("site", now.AddDays(-40), up: true, responseMs: 10);
        _store.Record("site", now, up: true, responseMs: 10);

        _store.PruneOlderThan(TimeSpan.FromDays(30));

        var uptime = _store.GetUptime("site", TimeSpan.FromDays(365));
        Assert.Equal(100.0, uptime!.Value.Percent);
    }
}
