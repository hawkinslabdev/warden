using Microsoft.Extensions.Logging.Abstractions;
using Warden.Configuration;
using Warden.Services;
using Warden.Services.Admin;

namespace Warden.Tests;

/// <summary>Git declares; admin store wins when saved.</summary>
public sealed class ContentServiceAdminOverrideTests : IDisposable
{
    private readonly string _contentDir = Path.Combine(Path.GetTempPath(), "warden-merge-content-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _dbDir = Path.Combine(Path.GetTempPath(), "warden-merge-db-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly AdminOverrideStore _store;
    private readonly ContentService _service;

    public ContentServiceAdminOverrideTests()
    {
        Directory.CreateDirectory(_contentDir);
        Directory.CreateDirectory(_dbDir);
        _store = new AdminOverrideStore(new MonitoringOptions { DatabasePath = Path.Combine(_dbDir, "warden.db") });
        _service = new ContentService(
            new DocsOptions { RootPath = _contentDir, EnableHotReload = false },
            new MarkdownService(),
            NullLogger<ContentService>.Instance,
            _store);
    }

    // Simulates a git pull landing.
    private void WriteGitConfig(string monitoringJson) =>
        File.WriteAllText(Path.Combine(_contentDir, "config.json"), $$"""{ "monitoring": { "targets": [{{monitoringJson}}] } } """);

    private async Task<bool?> RebuildAndGetEnabled(string id)
    {
        await _service.ForceRebuildAsync(CancellationToken.None);
        return _service.SiteConfig?.Monitoring?.Targets?.FirstOrDefault(t => t.Id == id)?.Enabled;
    }

    [Fact]
    public async Task GitEnabledWithNoOverride_StaysEnabled()
    {
        WriteGitConfig("""{ "id": "a", "name": "A" }""");

        Assert.Null(await RebuildAndGetEnabled("a")); // null == enabled, the default
    }

    [Fact]
    public async Task GitEnabled_BackendDisables_OverrideWins()
    {
        WriteGitConfig("""{ "id": "a", "name": "A" }""");
        await _store.SetMonitoringAsync(Blob("""{ "id": "a", "enabled": false }"""), CancellationToken.None);

        Assert.Equal(false, await RebuildAndGetEnabled("a"));
    }

    [Fact]
    public async Task GitAlsoDisables_StaysDisabledBothAgree()
    {
        WriteGitConfig("""{ "id": "a", "name": "A", "enabled": false }""");
        await _store.SetMonitoringAsync(Blob("""{ "id": "a", "enabled": false }"""), CancellationToken.None);

        Assert.Equal(false, await RebuildAndGetEnabled("a"));
    }

    /// <summary>Later git pull must not resurrect override.</summary>
    [Fact]
    public async Task GitFlipsBackToEnabled_OverrideStillWins()
    {
        WriteGitConfig("""{ "id": "a", "name": "A" }""");
        await _store.SetMonitoringAsync(Blob("""{ "id": "a", "enabled": false }"""), CancellationToken.None);
        Assert.Equal(false, await RebuildAndGetEnabled("a"));

        // Git re-enables it; pull picks up.
        WriteGitConfig("""{ "id": "a", "name": "A", "enabled": true }""");

        Assert.Equal(false, await RebuildAndGetEnabled("a"));
    }

    [Fact]
    public async Task OperatorReEnablesAfterGitFlip_TheLatestAdminSaveWins()
    {
        WriteGitConfig("""{ "id": "a", "name": "A" }""");
        await _store.SetMonitoringAsync(Blob("""{ "id": "a", "enabled": false }"""), CancellationToken.None);
        Assert.Equal(false, await RebuildAndGetEnabled("a"));

        // Operator re-enables; full row re-saved.
        await _store.SetMonitoringAsync(Blob("""{ "id": "a" }"""), CancellationToken.None);

        Assert.Null(await RebuildAndGetEnabled("a"));
    }

    [Fact]
    public async Task NewGitTargetNeverSavedByAnAdmin_UsesGitsOwnValue()
    {
        WriteGitConfig("""{ "id": "a", "name": "A" }, { "id": "b", "name": "B", "enabled": false }""");
        await _store.SetMonitoringAsync(Blob("""{ "id": "a", "enabled": false }"""), CancellationToken.None);

        Assert.Equal(false, await RebuildAndGetEnabled("a"));
        Assert.Equal(false, await RebuildAndGetEnabled("b"));
    }

    [Fact]
    public async Task TargetRemovedFromGit_LeftoverOverrideRowIsHarmless()
    {
        WriteGitConfig("""{ "id": "a", "name": "A" }""");
        await _store.SetMonitoringAsync(Blob(
            """{ "id": "a", "enabled": false }""", """{ "id": "gone", "enabled": false }"""),
            CancellationToken.None);
        await _service.ForceRebuildAsync(CancellationToken.None);

        var ids = _service.SiteConfig!.Monitoring!.Targets!.Select(t => t.Id).ToList();
        Assert.Equal(["a"], ids);
    }

    [Fact]
    public async Task SavedOrderSurvivesAGitPullThatOnlyReordersTheFile()
    {
        WriteGitConfig("""{ "id": "a", "name": "A" }, { "id": "b", "name": "B" }""");
        await _store.SetMonitoringAsync(Blob("""{ "id": "b" }""", """{ "id": "a" }"""), CancellationToken.None);
        await _service.ForceRebuildAsync(CancellationToken.None);
        Assert.Equal(["b", "a"], _service.SiteConfig!.Monitoring!.Targets!.Select(t => t.Id));

        // Pull reorders file; saved order wins.
        WriteGitConfig("""{ "id": "a", "name": "A" }, { "id": "b", "name": "B" }""");
        await _service.ForceRebuildAsync(CancellationToken.None);
        Assert.Equal(["b", "a"], _service.SiteConfig!.Monitoring!.Targets!.Select(t => t.Id));
    }

    private static System.Text.Json.Nodes.JsonObject Blob(params string[] targets) =>
        (System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(
            $$"""{ "targets": [{{string.Join(",", targets)}}] }""")!;

    public void Dispose()
    {
        _service.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_contentDir)) Directory.Delete(_contentDir, recursive: true);
        if (Directory.Exists(_dbDir)) try { Directory.Delete(_dbDir, recursive: true); } catch (IOException) { }
    }
}
