using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Warden.Configuration;

namespace Warden.Services.Admin;

// local overrides; git can't touch them.
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

        // one-time setup; sync is fine here.
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
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

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }

    /// <summary>null if unset or corrupt.</summary>
    public async Task<JsonObject?> GetMonitoringAsync(CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var select = connection.CreateCommand();
        select.CommandText = "SELECT json FROM admin_overrides WHERE key = 'monitoring';";
        await using var reader = await select.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        try { return JsonNode.Parse(reader.GetString(0)) as JsonObject; }
        catch (System.Text.Json.JsonException) { return null; }
    }

    public async Task SetMonitoringAsync(JsonObject monitoring, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var upsert = connection.CreateCommand();
        upsert.CommandText = """
            INSERT INTO admin_overrides (key, json) VALUES ('monitoring', $json)
            ON CONFLICT(key) DO UPDATE SET json = excluded.json;
            """;
        upsert.Parameters.AddWithValue("$json", monitoring.ToJsonString());
        await upsert.ExecuteNonQueryAsync(ct);
    }
}
