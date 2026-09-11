using System.Text.Json.Nodes;

namespace Warden.Services.Admin;

// Edits stored overrides as raw JSON.
public sealed class AdminConfigWriter(AdminOverrideStore store, ILogger<AdminConfigWriter> logger)
{
    // ponytail: single lock; per-operator locks if needed.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<bool> UpdateMonitoringAsync(Action<JsonObject> mutate, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var monitoring = store.GetMonitoring() ?? [];
            mutate(monitoring);
            await store.SetMonitoringAsync(monitoring, ct);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not save the admin config change");
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

    // Creates a bare row if none saved yet.
    public static JsonObject GetOrAddTarget(JsonObject monitoring, string id)
    {
        if (monitoring["targets"] is not JsonArray targets)
            monitoring["targets"] = targets = [];
        if (FindTarget(monitoring, id) is { } existing)
            return existing;

        var created = new JsonObject { ["id"] = id };
        targets.Add(created);
        return created;
    }

    // Omits key when value is default.
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
