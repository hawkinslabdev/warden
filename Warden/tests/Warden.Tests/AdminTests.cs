using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Warden.Configuration;
using Warden.Endpoints;
using Warden.Models;
using Warden.Services;
using Warden.Services.Admin;
using Warden.Services.Layout;
using Warden.Services.Theming;

namespace Warden.Tests;

public sealed class AdminConfigWriterTests : IDisposable
{
    private readonly string _dbDir = Path.Combine(Path.GetTempPath(), "warden-admin-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly AdminOverrideStore _store;

    public AdminConfigWriterTests()
    {
        Directory.CreateDirectory(_dbDir);
        _store = new AdminOverrideStore(new MonitoringOptions { DatabasePath = Path.Combine(_dbDir, "warden.db") });
    }

    private AdminConfigWriter Writer() => new(_store, NullLogger<AdminConfigWriter>.Instance);

    [Fact]
    public async Task PreservesKeysTheModelDoesNotKnow()
    {
        var writer = Writer();
        await _store.SetMonitoringAsync(
            (JsonObject)JsonNode.Parse("""{ "intervalSeconds": 60, "somethingWardenNeverParses": { "keep": [1, 2, 3] } }""")!,
            CancellationToken.None);

        Assert.True(await writer.UpdateMonitoringAsync(m => m["intervalSeconds"] = 120, CancellationToken.None));

        var saved = (await _store.GetMonitoringAsync(CancellationToken.None))!;
        Assert.Equal(3, ((JsonArray)saved["somethingWardenNeverParses"]!["keep"]!).Count);
        Assert.Equal(120, (int?)saved["intervalSeconds"]);
    }

    [Fact]
    public async Task CreatesTheBlobWhenNothingWasEverSaved()
    {
        var writer = Writer();

        Assert.True(await writer.UpdateMonitoringAsync(m => m["intervalSeconds"] = 30, CancellationToken.None));
        Assert.Equal(30, (int?)(await _store.GetMonitoringAsync(CancellationToken.None))!["intervalSeconds"]);
    }

    [Fact]
    public async Task ARowThatFailsToParseIsTreatedAsEmptyRatherThanCrashing()
    {
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = Path.Combine(_dbDir, "warden.db") }.ToString()))
        {
            await connection.OpenAsync();
            var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO admin_overrides (key, json) VALUES ('monitoring', 'not json');";
            await insert.ExecuteNonQueryAsync();
        }

        Assert.Null(await _store.GetMonitoringAsync(CancellationToken.None));
        var writer = Writer();
        Assert.True(await writer.UpdateMonitoringAsync(m => m["intervalSeconds"] = 5, CancellationToken.None));
        Assert.Equal(5, (int?)(await _store.GetMonitoringAsync(CancellationToken.None))!["intervalSeconds"]);
    }

    [Fact]
    public async Task ConcurrentWritesAllLandAndLeaveValidJson()
    {
        var writer = Writer();
        await _store.SetMonitoringAsync((JsonObject)JsonNode.Parse("""{ "targets": [] }""")!, CancellationToken.None);

        await Task.WhenAll(Enumerable.Range(0, 40).Select(i =>
            writer.UpdateMonitoringAsync(m => m["intervalSeconds"] = 10 + i, CancellationToken.None)));

        var value = (int?)(await _store.GetMonitoringAsync(CancellationToken.None))!["intervalSeconds"];
        Assert.InRange(value!.Value, 10, 49);
    }

    [Fact]
    public void FlagsAndIntsRemoveTheKeyAtTheirDefault()
    {
        var target = new JsonObject { ["id"] = "a", ["hidden"] = true, ["enabled"] = false, ["retries"] = 3 };

        AdminConfigWriter.SetFlag(target, "hidden", value: false, omitWhen: false);
        AdminConfigWriter.SetFlag(target, "enabled", value: true, omitWhen: true);
        AdminConfigWriter.SetInt(target, "retries", null);

        Assert.False(target.ContainsKey("hidden"));
        Assert.False(target.ContainsKey("enabled"));
        Assert.False(target.ContainsKey("retries"));
    }

    [Fact]
    public void FindTargetMatchesOnIdAndToleratesAMissingArray()
    {
        var monitoring = new JsonObject
        {
            ["targets"] = new JsonArray(new JsonObject { ["id"] = "a" }, new JsonObject { ["id"] = "b" }),
        };

        Assert.NotNull(AdminConfigWriter.FindTarget(monitoring, "b"));
        Assert.Null(AdminConfigWriter.FindTarget(monitoring, "c"));
        Assert.Null(AdminConfigWriter.FindTarget([], "a"));
    }

    [Fact]
    public void GetOrAddTargetCreatesABareRowOnlyWhenMissing()
    {
        JsonObject monitoring = [];

        var created = AdminConfigWriter.GetOrAddTarget(monitoring, "a");
        created["hidden"] = true;
        var again = AdminConfigWriter.GetOrAddTarget(monitoring, "a");

        Assert.Same(created, again);
        Assert.Single((JsonArray)monitoring["targets"]!);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dbDir))
            try { Directory.Delete(_dbDir, recursive: true); } catch (IOException) { }
    }
}

public sealed class IncidentDraftTests
{
    private static IncidentDraft Draft(string title = "API responses degraded", bool maintenance = false, bool degraded = true, string body = "Investigating.") =>
        new(title,
            new DateTimeOffset(2026, 8, 18, 7, 20, 0, TimeSpan.Zero),
            maintenance ? new DateTimeOffset(2026, 8, 18, 9, 0, 0, TimeSpan.Zero) : null,
            maintenance,
            ["forgejo"],
            degraded,
            "Some API requests are timing out.",
            body);

    [Fact]
    public void MarkdownMatchesTheIncidentFrontMatterWardenParses()
    {
        var markdown = Draft().Markdown();

        Assert.StartsWith("---\n", markdown, StringComparison.Ordinal);
        Assert.Contains("date: 2026-08-18T07:20:00Z", markdown, StringComparison.Ordinal);
        Assert.Contains("monitors: [forgejo]", markdown, StringComparison.Ordinal);
        Assert.Contains("status: degraded", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("maintenance:", markdown, StringComparison.Ordinal);
        Assert.EndsWith("Investigating.\n", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void AMaintenanceWindowCarriesEndAndDropsTheDegradedStatus()
    {
        var markdown = Draft(maintenance: true).Markdown();

        Assert.Contains("end: 2026-08-18T09:00:00Z", markdown, StringComparison.Ordinal);
        Assert.Contains("maintenance: true", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("status: degraded", markdown, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Title: with a colon")]
    [InlineData("- leading dash")]
    [InlineData("quote \" inside")]
    [InlineData("back\\slash")]
    public void TitlesThatWouldReparseAsYamlStructureAreQuoted(string title)
    {
        var line = Draft(title).Markdown().Split('\n').First(l => l.StartsWith("title:", StringComparison.Ordinal));

        Assert.StartsWith("title: \"", line, StringComparison.Ordinal);
        Assert.EndsWith("\"", line, StringComparison.Ordinal);
    }

    [Fact]
    public void SlugsAreUrlSafeAndBounded()
    {
        Assert.Equal("api-responses-degraded", Draft().Slug());
        Assert.Equal("incident", Draft("!!! ???").Slug());
        Assert.True(Draft(new string('x', 400)).Slug().Length <= 60);
        Assert.DoesNotContain("/", Draft("a/b/c").Slug(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://github.com/owner/repo.git", "https://github.com/owner/repo/new/main/incidents")]
    [InlineData("https://github.com/owner/repo", "https://github.com/owner/repo/new/main/incidents")]
    [InlineData("https://forgejo.example/owner/repo.git", "https://forgejo.example/owner/repo/_new/main/incidents")]
    [InlineData("https://git.example:3000/o/r.git", "https://git.example:3000/o/r/_new/main/incidents")]
    public void ForgeUrlUsesTheRightNewFileSegmentPerHost(string repo, string expected)
    {
        var auth = new AuthOptions { GitConfigured = true, RepoUrl = repo };
        Assert.Equal(expected, IncidentDraft.ResolveBase(auth));
    }

    [Fact]
    public void NoForgeUrlWithoutAConfiguredRemote()
    {
        Assert.Null(IncidentDraft.ResolveBase(new AuthOptions { GitConfigured = false, RepoUrl = "https://github.com/o/r" }));
        Assert.Null(IncidentDraft.ResolveBase(new AuthOptions { GitConfigured = true, RepoUrl = null }));
        Assert.Null(IncidentDraft.ResolveBase(new AuthOptions { GitConfigured = true, RepoUrl = "git@github.com:o/r.git" }));
    }

    [Fact]
    public void AnExplicitOverrideWinsOverTheDerivedUrl()
    {
        var auth = new AuthOptions { GitConfigured = true, RepoUrl = "https://github.com/o/r", NewFileUrl = "https://elsewhere/new/" };
        Assert.Equal("https://elsewhere/new", IncidentDraft.ResolveBase(auth));
    }

    [Fact]
    public void ABodyTooLongToPrefillReturnsNoUrlRatherThanATruncatedOne()
    {
        var auth = new AuthOptions { GitConfigured = true, RepoUrl = "https://github.com/o/r" };

        Assert.NotNull(Draft().NewFileUrl(auth));
        Assert.Null(Draft(body: new string('x', IncidentDraft.MaxPrefillLength)).NewFileUrl(auth));
    }
}

public sealed class AuthOptionsTests
{
    private static AuthOptions With(string? issuer, string? id = "client", string? secret = "secret") =>
        new() { Issuer = issuer, ClientId = id, ClientSecret = secret, AllowedSubjects = ["operator"] };

    [Theory]
    [InlineData("https://idp.example", true)]
    [InlineData("http://localhost:8080", true)]
    [InlineData("http://127.0.0.1:8080", true)]
    [InlineData("http://idp.example", false)]
    [InlineData("not-a-url", false)]
    [InlineData(null, false)]
    public void OnlyHttpsOrLoopbackIssuersEnableThePanel(string? issuer, bool expected) =>
        Assert.Equal(expected, With(issuer).Enabled);

    [Fact]
    public void BothClientIdAndSecretAreRequired()
    {
        Assert.False(With("https://idp.example", id: null).Enabled);
        Assert.False(With("https://idp.example", secret: null).Enabled);
        Assert.False(With("https://idp.example", id: "", secret: "").Enabled);
    }

    [Fact]
    public void AStaticExportNeverGetsAnAdminSurface() =>
        Assert.False((With("https://idp.example") with { IsExport = true }).AdminEnabled);

    [Fact]
    public void AnEmptyAllowlistKeepsThePanelOffRatherThanAdmittingEveryone()
    {
        // regression: an unset OIDC_ALLOWED_SUBJECTS used to admit every subject the issuer would
        // mint a token for, which is fail-open on any shared or self-signup IdP
        var auth = With("https://idp.example") with { AllowedSubjects = [] };

        Assert.False(auth.Enabled);
        Assert.False(auth.AdminEnabled);
        Assert.False(auth.Permits("anyone"));
        Assert.False(auth.Permits(null));
        Assert.False(auth.Permits(""));
    }

    [Fact]
    public void ANonEmptyAllowlistIsAnExactOrdinalMatch()
    {
        var auth = With("https://idp.example") with { AllowedSubjects = ["alice", "bob"] };

        Assert.True(auth.Permits("alice"));
        Assert.False(auth.Permits("Alice"));
        Assert.False(auth.Permits("alice2"));
        Assert.False(auth.Permits(null));
    }
}

public sealed class AuthEndpointGuardTests
{
    [Theory]
    [InlineData("/admin", "/admin")]
    [InlineData("/admin?flash=saved", "/admin?flash=saved")]
    [InlineData(null, "/admin")]
    [InlineData("", "/admin")]
    [InlineData("//evil.example", "/admin")]
    [InlineData("/\\evil.example", "/admin")]
    [InlineData("https://evil.example", "/admin")]
    [InlineData("javascript:alert(1)", "/admin")]
    public void ReturnUrlOnlyEverStaysLocal(string? candidate, string expected) =>
        Assert.Equal(expected, AuthEndpoints.SafeReturnUrl(candidate));

    // fallback now respects admin_path.
    [Fact]
    public void InvalidReturnUrlFallsBackToTheConfiguredAdminPath() =>
        Assert.Equal("/_/settings", AuthEndpoints.SafeReturnUrl(null, "", "/_/settings"));

    [Theory]
    [InlineData("https://hooks.example/x", true)]
    [InlineData("http://hooks.example/x", true)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("/relative", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("", false)]
    public void WebhookUrlsMustBeAbsoluteHttp(string url, bool expected) =>
        Assert.Equal(expected, AdminEndpoints.IsPostableUrl(url));
}

public sealed class WebhookCooldownTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ZeroCooldownNeverSuppresses() =>
        Assert.False(MonitorScheduler.WithinCooldown(Now.AddSeconds(-1), Now, TimeSpan.Zero));

    [Fact]
    public void AFirstOutageIsNeverSuppressed() =>
        Assert.False(MonitorScheduler.WithinCooldown(null, Now, TimeSpan.FromMinutes(15)));

    [Fact]
    public void ARepeatInsideTheWindowIsSuppressed() =>
        Assert.True(MonitorScheduler.WithinCooldown(Now.AddMinutes(-5), Now, TimeSpan.FromMinutes(15)));

    [Fact]
    public void ARepeatPastTheWindowFiresAgain() =>
        Assert.False(MonitorScheduler.WithinCooldown(Now.AddMinutes(-20), Now, TimeSpan.FromMinutes(15)));

    [Fact]
    public void TheBoundaryItselfFires() =>
        Assert.False(MonitorScheduler.WithinCooldown(Now.AddMinutes(-15), Now, TimeSpan.FromMinutes(15)));

    [Fact]
    public void CooldownReadsFromConfigAndFloorsAtZero()
    {
        Assert.Equal(TimeSpan.Zero, MonitorScheduler.WebhookCooldown(null));
        Assert.Equal(TimeSpan.FromMinutes(15), MonitorScheduler.WebhookCooldown(Monitoring(15)));
        Assert.Equal(TimeSpan.Zero, MonitorScheduler.WebhookCooldown(Monitoring(-5)));
    }

    private static MonitoringConfig Monitoring(int cooldown) =>
        new(null, null, null, null, null, null, null, null, WebhookCooldownMinutes: cooldown);
}

public sealed class MonitorEnablementTests : IDisposable
{
    private readonly string _dbDir = Path.Combine(Path.GetTempPath(), "warden-hb-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly HeartbeatStore _store;

    public MonitorEnablementTests()
    {
        Directory.CreateDirectory(_dbDir);
        _store = new HeartbeatStore(
            new MonitoringOptions { DatabasePath = Path.Combine(_dbDir, "warden.db") },
            NullLogger<HeartbeatStore>.Instance);
    }

    [Fact]
    public void ResetRemovesOnlyTheNamedMonitorsRows()
    {
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            _store.Record("keep", now.AddMinutes(-i), up: true, 10);
            _store.Record("drop", now.AddMinutes(-i), up: true, 10);
        }

        Assert.Equal(5, _store.DeleteFor("drop"));
        Assert.Null(_store.GetLatest("drop"));
        Assert.NotNull(_store.GetLatest("keep"));
    }

    [Fact]
    public void ResettingAnIdWithNoHistoryIsAHarmlessNoOp() =>
        Assert.Equal(0, _store.DeleteFor("never-seen"));

    [Fact]
    public void DisabledTargetsDropOutOfThePublicSetWhileHiddenOnesOnlyDropFromTheView()
    {
        List<MonitorTarget> targets =
        [
            new("plain", "Plain"),
            new("hidden", "Hidden") { Hidden = true },
            new("off", "Off") { Enabled = false },
        ];

        var scheduled = targets.Where(t => t.Enabled != false).Select(t => t.Id).ToList();
        var published = targets.Where(t => t.Hidden != true && t.Enabled != false).Select(t => t.Id).ToList();

        Assert.Equal(["plain", "hidden"], scheduled);
        Assert.Equal(["plain"], published);
    }

    public void Dispose()
    {
        _store.GetType();
        SqliteConnectionsClosed();
        if (Directory.Exists(_dbDir))
            try { Directory.Delete(_dbDir, recursive: true); } catch (IOException) { }
    }

    private static void SqliteConnectionsClosed() => Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
}

public sealed class AdminStressTests : IDisposable
{
    private readonly string _dbDir = Path.Combine(Path.GetTempPath(), "warden-stress-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly AdminOverrideStore _store;

    public AdminStressTests()
    {
        Directory.CreateDirectory(_dbDir);
        _store = new AdminOverrideStore(new MonitoringOptions { DatabasePath = Path.Combine(_dbDir, "warden.db") });
    }

    private AdminConfigWriter Writer() => new(_store, NullLogger<AdminConfigWriter>.Instance);

    [Fact]
    public async Task TwoHundredConcurrentEditsLeaveEveryTargetIntactAndTheBlobParseable()
    {
        var writer = Writer();
        var targets = new JsonArray();
        for (var i = 0; i < 20; i++)
            targets.Add(new JsonObject { ["id"] = $"m{i}" });
        await _store.SetMonitoringAsync(new JsonObject { ["targets"] = targets }, CancellationToken.None);

        await Task.WhenAll(Enumerable.Range(0, 200).Select(i => writer.UpdateMonitoringAsync(m =>
        {
            AdminConfigWriter.SetInt(AdminConfigWriter.GetOrAddTarget(m, $"m{i % 20}"), "retries", i % 7);
        }, CancellationToken.None)));

        var saved = (JsonArray)(await _store.GetMonitoringAsync(CancellationToken.None))!["targets"]!;
        Assert.Equal(20, saved.Count);
        Assert.All(saved, node => Assert.NotNull((string?)node!["id"]));
    }

    [Fact]
    public async Task AReaderRacingTheWriterNeverSeesCorruptJson()
    {
        var writer = Writer();
        await _store.SetMonitoringAsync((JsonObject)JsonNode.Parse("""{ "targets": [] }""")!, CancellationToken.None);

        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var reader = Task.Run(async () =>
        {
            var reads = 0;
            while (!stop.IsCancellationRequested)
                if (await _store.GetMonitoringAsync(CancellationToken.None) is not null) reads++;
            return reads;
        }, CancellationToken.None);

        for (var i = 0; i < 150 && !stop.IsCancellationRequested; i++)
            await writer.UpdateMonitoringAsync(m => m["intervalSeconds"] = 10 + i, CancellationToken.None);

        stop.Cancel();
        Assert.True(await reader > 0);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dbDir))
            try { Directory.Delete(_dbDir, recursive: true); } catch (IOException) { }
    }
}

public sealed class IncidentDraftHostileInputTests
{
    private static IncidentDraft Draft(string title = "t", string? description = null, string body = "b", IReadOnlyList<string>? monitors = null) =>
        new(title, DateTimeOffset.UnixEpoch, null, false, monitors ?? [], false, description, body);

    [Theory]
    [InlineData("evil\nmaintenance: true")]
    [InlineData("evil\r\nstatus: degraded")]
    [InlineData("---\ntitle: injected")]
    [InlineData("\" \nmonitors: [all]\nx: \"")]
    public void ATitleCannotSmuggleExtraFrontMatterKeys(string title)
    {
        var lines = Draft(title).Markdown().Split("---\n")[1].Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // survives inside the quoted scalar, but never as its own key
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("title: \"", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("date: ", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void ADescriptionCannotSmuggleFrontMatterEither()
    {
        var lines = Draft(description: "x\nmaintenance: true").Markdown().Split("---\n")[1].Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("description: \"", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void MonitorIdsAreEmittedUnquoted_SoOnlyConfiguredIdsMayEverReachThem()
    {
        // unquoted, so the endpoint filters this list against the configured ids
        Assert.Contains("monitors: [a, b]", Draft(monitors: ["a", "b"]).Markdown(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyOrPunctuationOnlyTitleStillYieldsAUsableFilename()
    {
        Assert.Equal("incident", Draft("").Slug());
        Assert.Equal("incident", Draft("///").Slug());
        Assert.Equal("incident", Draft("---").Slug());
    }

    [Fact]
    public void SlugsNeverStartOrEndWithASeparator()
    {
        foreach (var title in new[] { " leading", "trailing ", "--dashes--", "a  b" })
        {
            var slug = Draft(title).Slug();
            Assert.False(slug.StartsWith('-'), title);
            Assert.False(slug.EndsWith('-'), title);
        }
    }

    [Fact]
    public void AnOversizedDraftDegradesToNoUrlRatherThanThrowing()
    {
        var auth = new AuthOptions { GitConfigured = true, RepoUrl = "https://github.com/o/r" };
        var huge = Draft(body: new string('x', 500_000));

        Assert.Null(huge.NewFileUrl(auth));
        Assert.True(huge.Markdown().Length > 500_000);
    }

    [Fact]
    public void CrlfBodiesAreNormalisedSoTheFileIsNotDoubleSpaced()
    {
        Assert.EndsWith("one\ntwo\n", Draft(body: "one\r\ntwo\r\n").Markdown(), StringComparison.Ordinal);
    }
}

/// <summary>The whole admin surface must be invisible on a deployment that never configured OIDC.</summary>
public sealed class AdminDisabledIntegrationTests : IClassFixture<WardenWebApplicationFactory>
{
    private readonly WardenWebApplicationFactory _factory;

    public AdminDisabledIntegrationTests(WardenWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/settings")]
    [InlineData("/auth/login")]
    [InlineData("/auth/callback")]
    public async Task AdminRoutesDoNotExistWithoutOidc(string path)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task NoLoginButtonAppearsInTheMasthead()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/", CancellationToken.None);

        Assert.DoesNotContain("auth-button", html, StringComparison.Ordinal);
        Assert.Contains("timezone-toggle", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostingToAnAdminRouteIsNotAnUnauthenticatedWriteEither()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/admin/reset",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("id", "anything")]), CancellationToken.None);

        // the content catch-all claims the path for GET, so an unmapped POST is 405
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"got {response.StatusCode}");
    }
}

public sealed class IncidentPathDerivationTests
{
    private static string Base(string incidentPath) =>
        IncidentDraft.ResolveBase(new AuthOptions
        {
            GitConfigured = true,
            RepoUrl = "https://forgejo.example/o/r.git",
            IncidentPath = incidentPath,
        })!;

    [Fact]
    public void TheCloneRootAndDocsRootBeingTheSameGivesABareIncidentsFolder() =>
        Assert.EndsWith("/_new/main/incidents", Base("incidents"), StringComparison.Ordinal);

    [Fact]
    public void ADocsRootNestedInTheCloneKeepsItsPrefix() =>
        Assert.EndsWith("/_new/main/content/incidents", Base("content/incidents"), StringComparison.Ordinal);

    [Fact]
    public void ADeeperNestingSurvivesToo() =>
        Assert.EndsWith("/_new/main/sites/status/incidents", Base("sites/status/incidents"), StringComparison.Ordinal);
}

public sealed class ResolveIncidentPathTests
{
    private static string Root(params string[] parts) =>
        Path.GetFullPath(Path.Combine([Path.GetTempPath(), .. parts]));

    [Fact]
    public void SameCloneAndDocsRootGivesTheBareFolder() =>
        Assert.Equal("incidents", AuthOptions.ResolveIncidentPath(Root("repo"), Root("repo")));

    [Fact]
    public void DocsRootNestedOneLevelKeepsItsPrefix() =>
        Assert.Equal("content/incidents", AuthOptions.ResolveIncidentPath(Root("repo"), Root("repo", "content")));

    [Fact]
    public void DocsRootNestedDeeperKeepsTheWholePrefix() =>
        Assert.Equal("sites/status/content/incidents",
            AuthOptions.ResolveIncidentPath(Root("repo"), Root("repo", "sites", "status", "content")));

    [Fact]
    public void DocsRootOutsideTheCloneFallsBackRatherThanEmittingDotDots() =>
        Assert.Equal("incidents", AuthOptions.ResolveIncidentPath(Root("repo"), Root("elsewhere")));

    [Fact]
    public void ATrailingSeparatorOnEitherRootChangesNothing() =>
        Assert.Equal("content/incidents",
            AuthOptions.ResolveIncidentPath(Root("repo") + Path.DirectorySeparatorChar, Root("repo", "content")));
}

/// <summary>A verbatim-string escaping slip in the stylesheet source (`content: ""` instead of
/// `content: """"`) emits an unterminated CSS string that silently kills every rule after it.</summary>
public sealed class GeneratedCssIntegrityTests
{
    public static TheoryData<string> ThemeNames() => [.. ThemeRegistry.All.Select(t => t.Name)];

    private static string Css(string name)
    {
        var theme = ThemeRegistry.Resolve(name);
        return LayoutProvider.GetStylesAsset(ThemeCssBuilder.BuildTokenCss(theme, ThemeMode.Auto), theme.ComponentCss, "").Body;
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void EveryStringLiteralIsClosed(string name)
    {
        var css = Css(name);

        Assert.True(css.Count(c => c == '"') % 2 == 0, "odd number of double quotes: a string literal is unterminated");
        Assert.True(css.Count(c => c == '\'') % 2 == 0, "odd number of single quotes: a string literal is unterminated");
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void BracesBalance(string name)
    {
        var css = Css(name);
        var depth = 0;

        foreach (var c in css)
        {
            if (c == '{') depth++;
            else if (c == '}') depth--;
            Assert.True(depth >= 0, "a closing brace arrives before its block opened");
        }

        Assert.Equal(0, depth);
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void NoDeclarationOpensAStringItNeverCloses(string name)
    {
        // `content: ";` is what the escaping slip actually emits
        var offenders = Css(name)
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(d => d.Count(c => c == '"') % 2 != 0)
            .Select(d => d.Trim())
            .ToArray();

        Assert.True(offenders.Length == 0, $"unbalanced quotes in: {string.Join(" | ", offenders.Take(3))}");
    }

    [Theory]
    [MemberData(nameof(ThemeNames))]
    public void TheAdminSurfaceRulesSurviveToTheEndOfTheSheet(string name)
    {
        var css = Css(name);

        // these sit after the toggle knob, so a broken string upstream deletes them
        Assert.Contains(".admin-switch-input", css, StringComparison.Ordinal);
        Assert.Contains(".admin-actionbar", css, StringComparison.Ordinal);
        Assert.Contains(".toast", css, StringComparison.Ordinal);
        Assert.Contains(".admin-dialog", css, StringComparison.Ordinal);
        Assert.Contains(".auth-button", css, StringComparison.Ordinal);
    }
}
