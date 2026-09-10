using System.Text.Json;
using System.Text.Json.Nodes;
using Warden.Configuration;

namespace Warden.Services.Admin;

// edits the monitoring block as a JsonNode tree; round-tripping Config would drop unmodelled keys
public sealed class AdminConfigWriter(DocsOptions docs, ILogger<AdminConfigWriter> logger)
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    // ponytail: one process-wide lock; per-file locks if this ever serves more than one operator
    private readonly SemaphoreSlim _gate = new(1, 1);

    public string ConfigPath => ContentService.ResolveJsonFile(Path.GetFullPath(docs.RootPath), "config.json");

    public async Task<bool> UpdateMonitoringAsync(Action<JsonObject> mutate, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var path = ConfigPath;
            JsonObject root;
            try
            {
                root = File.Exists(path)
                    ? JsonNode.Parse(await File.ReadAllTextAsync(path, ct)) as JsonObject ?? []
                    : [];
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Refusing to write {ConfigPath}: it does not parse as JSON", path);
                return false;
            }

            if (root["monitoring"] is not JsonObject monitoring)
            {
                monitoring = [];
                root["monitoring"] = monitoring;
            }

            mutate(monitoring);

            var temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, root.ToJsonString(WriteOptions) + "\n", ct);
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "Could not write the admin config change");
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public static JsonObject? FindTarget(JsonObject monitoring, string id) =>
        monitoring["targets"] as JsonArray is { } targets
            ? targets.OfType<JsonObject>().FirstOrDefault(t => (string?)t["id"] == id)
            : null;

    // removes the key at its default rather than writing "hidden": false
    public static void SetFlag(JsonObject target, string key, bool value, bool omitWhen)
    {
        if (value == omitWhen)
            target.Remove(key);
        else
            target[key] = value;
    }

    public static void SetInt(JsonObject target, string key, int? value)
    {
        if (value is null)
            target.Remove(key);
        else
            target[key] = value.Value;
    }
}
