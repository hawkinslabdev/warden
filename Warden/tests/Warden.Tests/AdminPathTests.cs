using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Warden.Tests;

public sealed class CustomAdminPathFactory : WardenWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("OIDC_ISSUER", "https://idp.invalid");
        Environment.SetEnvironmentVariable("OIDC_CLIENT_ID", "warden");
        Environment.SetEnvironmentVariable("OIDC_CLIENT_SECRET", "s3cret");
        Environment.SetEnvironmentVariable("OIDC_ALLOWED_SUBJECTS", "operator-1");
        Environment.SetEnvironmentVariable("ADMIN_PATH", "/_/auth");

        base.ConfigureWebHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        foreach (var name in new[] { "OIDC_ISSUER", "OIDC_CLIENT_ID", "OIDC_CLIENT_SECRET", "OIDC_ALLOWED_SUBJECTS", "ADMIN_PATH" })
            Environment.SetEnvironmentVariable(name, null);
        base.Dispose(disposing);
    }
}

/// <summary>admin_path moves panel; /admin gone.</summary>
public sealed class AdminPathTests : IClassFixture<CustomAdminPathFactory>
{
    private readonly CustomAdminPathFactory _factory;

    public AdminPathTests(CustomAdminPathFactory factory) => _factory = factory;

    // route resolves; not a 404.
    [Fact]
    public async Task TheCustomPathResolvesToTheAdminRoute()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/_/auth");

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheDefaultPathNoLongerExists()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/admin");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TheSignInLinkPointsAtTheCustomAdminPath()
    {
        using var client = _factory.CreateClient();
        var html = await client.GetStringAsync("/");

        Assert.Contains("href=\"/auth/login\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/admin\"", html, StringComparison.Ordinal);
    }
}
