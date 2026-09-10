using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Warden.Tests;

/// <summary>Stands in for a completed OIDC login so the panel itself can be exercised without an IdP.</summary>
public sealed class StubAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "StubOidc";

    public static string Subject { get; set; } = "operator-1";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("X-Test-SignedIn"))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identity = new ClaimsIdentity([new Claim("sub", Subject)], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public sealed class AdminWebApplicationFactory : WardenWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("OIDC_ISSUER", "https://idp.invalid");
        Environment.SetEnvironmentVariable("OIDC_CLIENT_ID", "warden");
        Environment.SetEnvironmentVariable("OIDC_CLIENT_SECRET", "s3cret");
        // an empty allowlist now keeps the panel off entirely, so the host has to name its operator
        Environment.SetEnvironmentVariable("OIDC_ALLOWED_SUBJECTS", StubAuthHandler.Subject);
        Environment.SetEnvironmentVariable("GIT_ENABLED", "true");
        Environment.SetEnvironmentVariable("GIT_URL", "https://forgejo.example/owner/repo.git");

        base.ConfigureWebHost(builder);

        File.WriteAllText(Path.Combine(ContentDir, "config.json"), """
            {
              "title": "Test Blog",
              "keepThisUnmodelledKey": true,
              "monitoring": {
                "intervalSeconds": 60,
                "targets": [
                  { "id": "alpha", "name": "Alpha", "url": "https://alpha.invalid" },
                  { "id": "beta", "name": "Beta", "url": "https://beta.invalid", "hidden": true }
                ]
              }
            }
            """);

        builder.ConfigureTestServices(services =>
            services.AddAuthentication(StubAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, StubAuthHandler>(StubAuthHandler.SchemeName, null));
    }

    public HttpClient SignedInClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Test-SignedIn", "1");
        return client;
    }

    public JsonObject ReadConfig() =>
        (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(ContentDir, "config.json")))!;

    protected override void Dispose(bool disposing)
    {
        foreach (var name in new[] { "OIDC_ISSUER", "OIDC_CLIENT_ID", "OIDC_CLIENT_SECRET", "OIDC_ALLOWED_SUBJECTS", "GIT_ENABLED", "GIT_URL" })
            Environment.SetEnvironmentVariable(name, null);
        base.Dispose(disposing);
    }
}

public sealed class AdminPageIntegrationTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    public AdminPageIntegrationTests(AdminWebApplicationFactory factory) => _factory = factory;

    private static async Task<string> AntiforgeryToken(HttpClient client)
    {
        var html = await client.GetStringAsync("/admin", CancellationToken.None);
        var marker = "name=\"__RequestVerificationToken\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        return html[start..html.IndexOf('"', start)];
    }

    [Fact]
    public async Task TheAdminPageRendersEverySection()
    {
        using var client = _factory.SignedInClient();

        var html = await client.GetStringAsync("/admin", CancellationToken.None);

        Assert.Contains(">Monitors<", html, StringComparison.Ordinal);
        Assert.Contains(">Webhooks<", html, StringComparison.Ordinal);
        Assert.Contains(">Timing<", html, StringComparison.Ordinal);
        Assert.Contains(">New incident or maintenance<", html, StringComparison.Ordinal);
        Assert.Contains("id=\"reset-dialog\"", html, StringComparison.Ordinal);
        Assert.Contains("operator-1", html, StringComparison.Ordinal);
        // hidden targets stay operable in the panel
        Assert.Contains("alpha", html, StringComparison.Ordinal);
        Assert.Contains("beta", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheAdminPageIsNoindexAndUncacheable()
    {
        using var client = _factory.SignedInClient();

        var response = await client.GetAsync("/admin", CancellationToken.None);
        var html = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Contains("no-store", response.Headers.CacheControl?.ToString() ?? "", StringComparison.Ordinal);
        Assert.Contains("noindex", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APostWithoutAnAntiforgeryTokenIsRejectedAndChangesNothing()
    {
        using var client = _factory.SignedInClient();
        var before = _factory.ReadConfig().ToJsonString();

        var response = await client.PostAsync("/admin/settings",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("intervalSeconds", "900")]), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("flash=stale-form", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, _factory.ReadConfig().ToJsonString());
    }

    [Fact]
    public async Task SavingTimingWritesConfigAndLeavesUnmodelledKeysAlone()
    {
        using var client = _factory.SignedInClient();
        var token = await AntiforgeryToken(client);

        var response = await client.PostAsync("/admin/settings", new FormUrlEncodedContent([
            new("__RequestVerificationToken", token),
            new("intervalSeconds", "120"),
            new("retentionDays", "45"),
            new("webhookCooldownMinutes", "15"),
        ]), CancellationToken.None);

        Assert.Contains("flash=saved", response.Headers.Location!.ToString(), StringComparison.Ordinal);

        var config = _factory.ReadConfig();
        Assert.Equal(120, (int?)config["monitoring"]!["intervalSeconds"]);
        Assert.Equal(45, (int?)config["monitoring"]!["retentionDays"]);
        Assert.Equal(15, (int?)config["monitoring"]!["webhookCooldownMinutes"]);
        Assert.True((bool?)config["keepThisUnmodelledKey"]);
        Assert.Equal("Test Blog", (string?)config["title"]);
    }

    [Fact]
    public async Task TimingValuesAreClampedRatherThanTrusted()
    {
        using var client = _factory.SignedInClient();
        var token = await AntiforgeryToken(client);

        await client.PostAsync("/admin/settings", new FormUrlEncodedContent([
            new("__RequestVerificationToken", token),
            new("intervalSeconds", "-99999"),
            new("retentionDays", "999999999"),
        ]), CancellationToken.None);

        var monitoring = _factory.ReadConfig()["monitoring"]!;
        Assert.Equal(5, (int?)monitoring["intervalSeconds"]);
        Assert.Equal(3650, (int?)monitoring["retentionDays"]);
    }

    [Fact]
    public async Task DisablingAMonitorWritesTheFlagAndDropsItFromTheStatusApi()
    {
        using var client = _factory.SignedInClient();
        var token = await AntiforgeryToken(client);

        await client.PostAsync("/admin/settings", new FormUrlEncodedContent([
            new("__RequestVerificationToken", token),
            new("monitorId", "alpha"),
            new("retries:alpha", "2"),
        ]), CancellationToken.None);

        var alpha = ((JsonArray)_factory.ReadConfig()["monitoring"]!["targets"]!)
            .OfType<JsonObject>().First(t => (string?)t["id"] == "alpha");
        Assert.False((bool?)alpha["enabled"]);
        Assert.Equal(2, (int?)alpha["retries"]);
    }

    [Fact]
    public async Task AnUnknownMonitorIdIsRefusedRatherThanAppended()
    {
        using var client = _factory.SignedInClient();
        var token = await AntiforgeryToken(client);

        var response = await client.PostAsync("/admin/settings", new FormUrlEncodedContent([
            new("__RequestVerificationToken", token),
            new("monitorId", "../../etc/passwd"),
            new("enabled:../../etc/passwd", "on"),
        ]), CancellationToken.None);

        Assert.Contains("flash=unknown-monitor", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal(2, ((JsonArray)_factory.ReadConfig()["monitoring"]!["targets"]!).Count);
    }

    [Fact]
    public async Task ResettingAMonitorThatIsNotConfiguredIsRefused()
    {
        using var client = _factory.SignedInClient();
        var token = await AntiforgeryToken(client);

        var response = await client.PostAsync("/admin/reset", new FormUrlEncodedContent([
            new("__RequestVerificationToken", token),
            new("id", "not-a-monitor"),
        ]), CancellationToken.None);

        Assert.Contains("flash=unknown-monitor", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARejectedWebhookUrlNeverReachesTheConfig()
    {
        using var client = _factory.SignedInClient();
        var token = await AntiforgeryToken(client);

        var response = await client.PostAsync("/admin/settings", new FormUrlEncodedContent([
            new("__RequestVerificationToken", token),
            new("url", "file:///etc/passwd"),
            new("headerName", ""),
            new("headerValue", ""),
        ]), CancellationToken.None);

        Assert.Contains("flash=bad-webhook-url", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("file:///etc/passwd", _factory.ReadConfig().ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWebhookSecretIsStoredOnceAndNeverEchoedBack()
    {
        using var client = _factory.SignedInClient();
        var token = await AntiforgeryToken(client);

        await client.PostAsync("/admin/settings", new FormUrlEncodedContent([
            new("__RequestVerificationToken", token),
            new("url", "https://hooks.invalid/alert"),
            new("headerName", "Authorization"),
            new("headerValue", "Bearer super-secret-value"),
        ]), CancellationToken.None);

        var stored = (JsonArray)_factory.ReadConfig()["monitoring"]!["webhooks"]!;
        Assert.Equal("Bearer super-secret-value", (string?)stored[0]!["headers"]!["Authorization"]);

        var html = await client.GetStringAsync("/admin", CancellationToken.None);
        Assert.DoesNotContain("super-secret-value", html, StringComparison.Ordinal);
        Assert.Contains("https://hooks.invalid/alert", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResubmittingWithABlankSecretKeepsTheStoredOne()
    {
        using var client = _factory.SignedInClient();
        var token = await AntiforgeryToken(client);

        await client.PostAsync("/admin/settings", new FormUrlEncodedContent([
            new("__RequestVerificationToken", token),
            new("url", "https://hooks.invalid/keep"),
            new("headerName", "Authorization"),
            new("headerValue", "Bearer keep-me"),
        ]), CancellationToken.None);

        token = await AntiforgeryToken(client);
        await client.PostAsync("/admin/settings", new FormUrlEncodedContent([
            new("__RequestVerificationToken", token),
            new("url", "https://hooks.invalid/keep"),
            new("headerName", "Authorization"),
            new("headerValue", ""),
        ]), CancellationToken.None);

        var stored = (JsonArray)_factory.ReadConfig()["monitoring"]!["webhooks"]!;
        Assert.Equal("Bearer keep-me", (string?)stored[0]!["headers"]!["Authorization"]);
    }

    [Fact]
    public async Task ComposingAnIncidentRedirectsToTheForgeNewFileForm()
    {
        using var client = _factory.SignedInClient();
        var token = await AntiforgeryToken(client);

        var response = await client.PostAsync("/admin/incident", new FormUrlEncodedContent([
            new("__RequestVerificationToken", token),
            new("title", "Database is slow"),
            new("start", "2026-09-08T10:00"),
            new("monitors", "alpha"),
            new("degraded", "on"),
            new("body", "Looking into it."),
        ]), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Uri.UnescapeDataString(response.Headers.Location!.ToString());
        Assert.StartsWith("https://forgejo.example/owner/repo/_new/main/incidents", location, StringComparison.Ordinal);
        Assert.Contains("filename=database-is-slow.md", location, StringComparison.Ordinal);
        Assert.Contains("monitors: [alpha]", location, StringComparison.Ordinal);
        Assert.Contains("status: degraded", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnIncidentCannotNameAMonitorThatIsNotConfigured()
    {
        using var client = _factory.SignedInClient();
        var token = await AntiforgeryToken(client);

        var response = await client.PostAsync("/admin/incident", new FormUrlEncodedContent([
            new("__RequestVerificationToken", token),
            new("title", "Injected"),
            new("start", "2026-09-08T10:00"),
            new("monitors", "alpha"),
            new("monitors", "]\nmaintenance: true\nx: ["),
            new("body", "b"),
        ]), CancellationToken.None);

        var location = Uri.UnescapeDataString(response.Headers.Location!.ToString());
        Assert.Contains("monitors: [alpha]", location, StringComparison.Ordinal);
        Assert.DoesNotContain("maintenance: true", location, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMaintenanceWindowWithoutAnEndIsRefused()
    {
        using var client = _factory.SignedInClient();
        var token = await AntiforgeryToken(client);

        var response = await client.PostAsync("/admin/incident", new FormUrlEncodedContent([
            new("__RequestVerificationToken", token),
            new("title", "Planned work"),
            new("start", "2026-09-08T10:00"),
            new("maintenance", "on"),
            new("body", "b"),
        ]), CancellationToken.None);

        Assert.Contains("flash=maintenance-needs-end", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEndBeforeTheStartIsRefused()
    {
        using var client = _factory.SignedInClient();
        var token = await AntiforgeryToken(client);

        var response = await client.PostAsync("/admin/incident", new FormUrlEncodedContent([
            new("__RequestVerificationToken", token),
            new("title", "Backwards"),
            new("start", "2026-09-08T12:00"),
            new("end", "2026-09-08T10:00"),
            new("body", "b"),
        ]), CancellationToken.None);

        Assert.Contains("flash=end-before-start", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnOversizedIncidentRendersTheFileInPlaceInsteadOfRedirecting()
    {
        using var client = _factory.SignedInClient();
        var token = await AntiforgeryToken(client);

        var response = await client.PostAsync("/admin/incident", new FormUrlEncodedContent([
            new("__RequestVerificationToken", token),
            new("title", "Long one"),
            new("start", "2026-09-08T10:00"),
            new("body", new string('x', 9000)),
        ]), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(CancellationToken.None);
        Assert.Contains("Incident file", html, StringComparison.Ordinal);
        Assert.Contains("title: &quot;Long one&quot;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAnonymousVisitorStillGetsThePublicPageWithASignInButton()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/", CancellationToken.None);

        Assert.Contains("auth-button", html, StringComparison.Ordinal);
        Assert.Contains("/auth/login", html, StringComparison.Ordinal);
        Assert.DoesNotContain("auth-button--active", html, StringComparison.Ordinal);
    }
}

public sealed class AdminReorderTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    public AdminReorderTests(AdminWebApplicationFactory factory) => _factory = factory;

    private static async Task<string> Token(HttpClient client)
    {
        var html = await client.GetStringAsync("/admin", CancellationToken.None);
        const string marker = "name=\"__RequestVerificationToken\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        return html[start..html.IndexOf('"', start)];
    }

    private string[] Order() =>
        [.. ((JsonArray)_factory.ReadConfig()["monitoring"]!["targets"]!)
            .OfType<JsonObject>().Select(t => (string?)t["id"] ?? "")];

    private async Task<HttpResponseMessage> Reorder(HttpClient client, string ids) =>
        await client.PostAsync("/admin/settings", new FormUrlEncodedContent([
            new("__RequestVerificationToken", await Token(client)),
            new("order", ids),
        ]), CancellationToken.None);

    [Fact]
    public async Task EveryMonitorRowCarriesADragHandleAndItsId()
    {
        using var client = _factory.SignedInClient();

        var html = await client.GetStringAsync("/admin", CancellationToken.None);

        Assert.Contains("id=\"monitor-order\"", html, StringComparison.Ordinal);
        Assert.Contains("data-monitor-id=\"alpha\"", html, StringComparison.Ordinal);
        Assert.Contains("data-grip", html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Reorder Alpha\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReorderingRewritesTheTargetsArray()
    {
        using var client = _factory.SignedInClient();
        var original = Order();

        await Reorder(client, string.Join(',', original.Reverse()));

        Assert.Equal(original.Reverse(), Order());
    }

    [Fact]
    public async Task AnUnmentionedTargetKeepsItsPlaceInsteadOfBeingDropped()
    {
        using var client = _factory.SignedInClient();
        var before = Order();

        await Reorder(client, before[^1]);

        Assert.Equal(before.Length, Order().Length);
        Assert.Equal(before[^1], Order()[0]);
    }

    [Fact]
    public async Task AnUnknownIdIsIgnoredWithoutLosingAnyTarget()
    {
        using var client = _factory.SignedInClient();
        var before = Order();

        await Reorder(client, "ghost," + string.Join(',', before));

        Assert.Equal(before, Order());
    }

    [Fact]
    public async Task ReorderingPreservesEachTargetsOtherFields()
    {
        using var client = _factory.SignedInClient();

        await Reorder(client, string.Join(',', Order().Reverse()));

        var beta = ((JsonArray)_factory.ReadConfig()["monitoring"]!["targets"]!)
            .OfType<JsonObject>().First(t => (string?)t["id"] == "beta");
        Assert.Equal("Beta", (string?)beta["name"]);
        Assert.True((bool?)beta["hidden"]);
    }

    [Fact]
    public async Task AnEmptyOrderLeavesTheStoredOrderAlone()
    {
        using var client = _factory.SignedInClient();
        var before = Order();

        var response = await Reorder(client, "");

        Assert.Contains("flash=saved", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, Order());
    }

    [Fact]
    public async Task ReorderingNeedsAnAntiforgeryTokenLikeEveryOtherWrite()
    {
        using var client = _factory.SignedInClient();
        var before = Order();

        var response = await client.PostAsync("/admin/settings",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("order", "beta,alpha")]), CancellationToken.None);

        Assert.Contains("flash=stale-form", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, Order());
    }
}
