using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json.Nodes;
using Warden.Configuration;
using Warden.Endpoints;
using Warden.Services;

namespace Warden.Tests;

/// <summary>Regression cover for the 2026-09 admin-surface audit. One test per finding.</summary>
public sealed class SecurityAuditFixTests
{
    // M1 — browsers strip these before parsing, turning "/<TAB>/host" into a protocol-relative "//host"
    public static TheoryData<string> OffOriginReturnUrls() =>
    [
        "/" + (char)0x09 + "/evil.example",
        "/" + (char)0x0A + "/evil.example",
        "/" + (char)0x0D + "/evil.example",
        "/" + (char)0x00 + "/evil.example",
        "/" + (char)0x09 + "\\evil.example",
        "//evil.example",
        "/\\evil.example",
        "/\\/evil.example",
        "https://evil.example",
        "javascript:alert(1)",
    ];

    [Theory]
    [MemberData(nameof(OffOriginReturnUrls))]
    public void AReturnUrlThatCanLeaveTheOriginFallsBackToAdmin(string candidate) =>
        Assert.Equal("/admin", AuthEndpoints.SafeReturnUrl(candidate));

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/settings")]
    [InlineData("/docs/page?a=1&b=2")]
    public void AnOrdinaryLocalPathIsStillHonoured(string candidate) =>
        Assert.Equal(candidate, AuthEndpoints.SafeReturnUrl(candidate));

    [Fact]
    public void AnOverlongReturnUrlIsRefusedRatherThanInflatingTheCorrelationCookie()
    {
        var tooLong = "/" + new string('a', AuthEndpoints.MaxReturnUrlLength);

        Assert.Equal("/admin", AuthEndpoints.SafeReturnUrl(tooLong));
        Assert.Equal("/" + new string('a', AuthEndpoints.MaxReturnUrlLength - 1),
            AuthEndpoints.SafeReturnUrl("/" + new string('a', AuthEndpoints.MaxReturnUrlLength - 1)));
    }

    // M2 — TryAddWithoutValidation writes CR/LF straight to the socket
    [Theory]
    [InlineData("X-Evil", "a\r\nX-Injected: yes")]
    [InlineData("X-Evil", "a\nX-Injected: yes")]
    [InlineData("X-Evil\r\nX-Injected", "value")]
    [InlineData("X-Evil", "a\r\n\r\nGET /admin HTTP/1.1")]
    [InlineData("", "value")]
    [InlineData("   ", "value")]
    public void AHeaderCarryingAControlCharacterIsRefused(string name, string value) =>
        Assert.False(MonitorScheduler.IsSafeHeader(name, value));

    [Theory]
    [InlineData("Authorization", "Bearer abc.def")]
    [InlineData("X-Signature", "sha256=deadbeef")]
    public void AnOrdinaryHeaderIsStillAccepted(string name, string value) =>
        Assert.True(MonitorScheduler.IsSafeHeader(name, value));

    [Fact]
    public void AHeaderWithNoValueIsAcceptedBecauseBlankMeansKeepTheStoredSecret() =>
        Assert.True(MonitorScheduler.IsSafeHeader("Authorization", null));

    // H1 — an unset allowlist used to admit every subject the issuer would mint a token for
    [Fact]
    public void AnUnsetAllowlistLeavesThePanelOff()
    {
        var auth = new AuthOptions
        {
            Issuer = "https://idp.example",
            ClientId = "client",
            ClientSecret = "secret",
            AllowedSubjects = [],
        };

        Assert.False(auth.Enabled);
        Assert.False(auth.AdminEnabled);
        Assert.False(auth.Permits("anyone"));
    }

    [Fact]
    public void NamingASubjectTurnsThePanelBackOn()
    {
        var auth = new AuthOptions
        {
            Issuer = "https://idp.example",
            ClientId = "client",
            ClientSecret = "secret",
            AllowedSubjects = ["operator-1"],
        };

        Assert.True(auth.AdminEnabled);
        Assert.True(auth.Permits("operator-1"));
        Assert.False(auth.Permits("operator-2"));
    }
}

/// <summary>The audit findings that only show up against a running host.</summary>
public sealed class SecurityAuditFixIntegrationTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    public SecurityAuditFixIntegrationTests(AdminWebApplicationFactory factory) => _factory = factory;

    private static async Task<string> Token(HttpClient client)
    {
        var html = await client.GetStringAsync("/admin", CancellationToken.None);
        const string marker = "name=\"__RequestVerificationToken\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        return html[start..html.IndexOf('"', start)];
    }

    // M2, at the admin boundary rather than the socket
    [Fact]
    public async Task AWebhookHeaderCarryingCrlfIsRefusedAndNothingIsWritten()
    {
        using var client = _factory.SignedInClient();
        var token = await Token(client);
        var before = _factory.ReadConfig().ToJsonString();

        var response = await client.PostAsync("/admin/settings", new FormUrlEncodedContent([
            new("__RequestVerificationToken", token),
            new("url", "https://hooks.invalid/alert"),
            new("headerName", "Authorization"),
            new("headerValue", "Bearer x\r\nX-Injected: yes"),
        ]), CancellationToken.None);

        Assert.Contains("flash=bad-webhook-header", response.Headers.Location!.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, _factory.ReadConfig().ToJsonString());
        Assert.DoesNotContain("X-Injected", _factory.ReadConfig().ToJsonString(), StringComparison.Ordinal);
    }

    // Nothing the admin surface holds may reach an anonymous caller. The fixture's "beta" target is
    // hidden:true and carries a webhook secret, so both are live canaries for a public leak.
    [Theory]
    [InlineData("/")]
    [InlineData("/api/status")]
    [InlineData("/sitemap.xml")]
    [InlineData("/robots.txt")]
    public async Task AnAnonymousCallerSeesNoAdminStateOnAPublicSurface(string path)
    {
        using var anonymous = _factory.CreateClient();

        var body = await anonymous.GetStringAsync(path, CancellationToken.None);

        Assert.DoesNotContain("__RequestVerificationToken", body, StringComparison.Ordinal);
        Assert.DoesNotContain("admin-settings", body, StringComparison.Ordinal);
        Assert.DoesNotContain("hooks.invalid", body, StringComparison.Ordinal);
        Assert.DoesNotContain("alpha.invalid", body, StringComparison.Ordinal);
        Assert.DoesNotContain("operator-1", body, StringComparison.Ordinal);
        // hidden targets are withheld from every public surface
        Assert.DoesNotContain("Beta", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheAdminPageItselfNeverReachesAnAnonymousCaller()
    {
        using var anonymous = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await anonymous.GetAsync("/admin", CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // the stub scheme challenges with 401 where real OIDC redirects; either way it is not the page
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("__RequestVerificationToken", body, StringComparison.Ordinal);
        Assert.DoesNotContain("admin-card", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Beta", body, StringComparison.Ordinal);
    }

    // L1 — minimal APIs only auto-validate antiforgery on form-bound endpoints, and logout binds none
    [Fact]
    public async Task LoggingOutWithoutAnAntiforgeryTokenIsRefused()
    {
        using var client = _factory.SignedInClient();

        var response = await client.PostAsync("/auth/logout", new FormUrlEncodedContent([]), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("flash=stale-form", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }
}

/// <summary>W1-4 and W3-1 need the public host, not the admin one.</summary>
public sealed class SecurityAuditPublicSurfaceTests : IClassFixture<WardenWebApplicationFactory>
{
    private readonly WardenWebApplicationFactory _factory;

    public SecurityAuditPublicSurfaceTests(WardenWebApplicationFactory factory) => _factory = factory;

    // W3-1 — both bodies embed an absolute origin taken from the Host header
    [Theory]
    [InlineData("/robots.txt")]
    [InlineData("/sitemap.xml")]
    public async Task AnOriginBearingSeoResponseVariesOnHost(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path, CancellationToken.None);

        Assert.Contains("Host", response.Headers.Vary, StringComparer.OrdinalIgnoreCase);
    }

    // W1-4 — an absolute page-next reached the href attribute unencoded
    [Fact]
    public void AnAbsoluteFrontMatterPageLinkCannotBreakOutOfTheHrefAttribute()
    {
        var href = PageRequestHandler.AbsoluteHref("https://x.test/\" onmouseover=\"alert(1)");

        Assert.NotNull(href);
        Assert.DoesNotContain("\"", href, StringComparison.Ordinal);
        Assert.Contains("&quot;", href, StringComparison.Ordinal);
    }

    [Fact]
    public void ARelativePageLinkIsLeftToUrlPathsSoItIsNotEncodedTwice() =>
        Assert.Null(PageRequestHandler.AbsoluteHref("guide/getting-started"));
}

/// <summary>H1 at the host level: a complete OIDC config with no allowlist must not expose the panel.</summary>
public sealed class AdminWithoutAnAllowlistFactory : WardenWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("OIDC_ISSUER", "https://idp.invalid");
        Environment.SetEnvironmentVariable("OIDC_CLIENT_ID", "warden");
        Environment.SetEnvironmentVariable("OIDC_CLIENT_SECRET", "s3cret");
        Environment.SetEnvironmentVariable("OIDC_ALLOWED_SUBJECTS", null);
        base.ConfigureWebHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var name in new[] { "OIDC_ISSUER", "OIDC_CLIENT_ID", "OIDC_CLIENT_SECRET" })
            Environment.SetEnvironmentVariable(name, null);
        base.Dispose(disposing);
    }
}

public sealed class AdminWithoutAnAllowlistTests : IClassFixture<AdminWithoutAnAllowlistFactory>
{
    private readonly AdminWithoutAnAllowlistFactory _factory;

    public AdminWithoutAnAllowlistTests(AdminWithoutAnAllowlistFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/settings")]
    [InlineData("/auth/login")]
    public async Task NoAdminRouteExistsUntilASubjectIsAllowlisted(string path)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(path, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ThePublicPageOffersNoSignInLinkEither()
    {
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/", CancellationToken.None);

        Assert.DoesNotContain("auth-button", html, StringComparison.Ordinal);
    }
}

/// <summary>Sign-in must not share a rate-limit bucket with the operator's admin actions.</summary>
public sealed class AuthRateLimitIsolationTests : IClassFixture<AdminWebApplicationFactory>
{
    private readonly AdminWebApplicationFactory _factory;

    public AuthRateLimitIsolationTests(AdminWebApplicationFactory factory) => _factory = factory;

    private string? PolicyFor(string route) =>
        _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == route)
            .Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;

    [Theory]
    [InlineData("/auth/login")]
    [InlineData("/auth/logout")]
    public void SignInRoutesUseTheirOwnBudget(string route) =>
        Assert.Equal(RateLimitPolicies.Auth, PolicyFor(route));

    [Fact]
    public void AdminWritesKeepTheAdminBudget() =>
        Assert.Equal(RateLimitPolicies.Admin, PolicyFor("/admin/settings"));

    [Fact]
    public void TheTwoBudgetsAreDistinctSoFloodingOneCannotCloseTheOther() =>
        Assert.NotEqual(PolicyFor("/auth/login"), PolicyFor("/admin/settings"));
}
