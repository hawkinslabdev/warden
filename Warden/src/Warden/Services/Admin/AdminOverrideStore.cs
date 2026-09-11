using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Warden.Configuration;

namespace Warden.Services.Admin;

// Local overrides; prevents git pull to ever conflict.
public sealed class AdminOverrideStore
{
    private readonly string _connectionString;

    public AdminOverrideStore(MonitoringOptions options)
    {
        var dbPath = Path.IsPathRooted(options.DatabasePath)
            ? options.DatabasePath
            : Path.Combine(AppContext.BaseDirectory, options.DatabasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();

        using var connection = Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL;";
        pragma.ExecuteNonQuery();
        using var create = connection.CreateCommand();
        create.CommandText = """
            CREATE TABLE IF NOT EXISTS admin_overrides (
                key TEXT PRIMARY KEY,
                json TEXT NOT NULL
            );
            """;
        create.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>Null if unset or corrupt.</summary>
    public JsonObject? GetMonitoring()
    {
        using var connection = Open();
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT json FROM admin_overrides WHERE key = 'monitoring';";
        using var reader = select.ExecuteReader();
        if (!reader.Read())
            return null;

        try { return JsonNode.Parse(reader.GetString(0)) as JsonObject; }
        catch (System.Text.Json.JsonException) { return null; }
    }

    public Task SetMonitoringAsync(JsonObject monitoring, CancellationToken ct)
    {
        using var connection = Open();
        using var upsert = connection.CreateCommand();
        upsert.CommandText = """
            INSERT INTO admin_overrides (key, json) VALUES ('monitoring', $json)
            ON CONFLICT(key) DO UPDATE SET json = excluded.json;
            """;
        upsert.Parameters.AddWithValue("$json", monitoring.ToJsonString());
        return upsert.ExecuteNonQueryAsync(ct);
    }
}
