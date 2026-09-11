using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Warden.Configuration;

namespace Warden.Endpoints;

internal static class AuthEndpoints
{
    public const string SubjectClaim = "sub";

    public const string AdminPolicy = "warden-admin";

    /// <summary>A returnUrl is a local path, and it is round-tripped through the OIDC correlation
    /// cookie. Anything longer than this is not a real path and only serves to inflate that cookie.</summary>
    internal const int MaxReturnUrlLength = 512;

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app, AuthOptions options)
    {
        app.MapGet($"{options.AuthPath}/login", (HttpContext ctx, string? returnUrl) =>
        {
            // the response carries a correlation Set-Cookie and is single-use; no cache may keep it
            ctx.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, private";
            ctx.Response.Headers.Pragma = "no-cache";
            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = SafeReturnUrl(returnUrl, ctx.Request.PathBase) },
                [OpenIdConnectDefaults.AuthenticationScheme]);
        }).RequireRateLimiting(RateLimitPolicies.Auth);

        // POST only; a GET logout fires from any third-party <img>. The token is validated by hand:
        // UseAntiforgery only auto-validates endpoints that bind a form, and this one binds none.
        app.MapPost($"{options.AuthPath}/logout", async (HttpContext ctx, IAntiforgery antiforgery) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(ctx);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.Redirect($"{ctx.Request.PathBase}{options.AdminPath}?flash=stale-form");
            }

            return Results.SignOut(
                new AuthenticationProperties { RedirectUri = $"{ctx.Request.PathBase}/" },
                [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
        }).RequireRateLimiting(RateLimitPolicies.Auth);

        return app;
    }

    // Open-redirect guard: only a local path is honoured. Browsers strip TAB/CR/LF from a URL before
    // parsing it, so "/<TAB>/evil.example" would otherwise reach the browser as "//evil.example" — a
    // protocol-relative URL pointing off-origin. Reject every control character, not just those three.
    internal static string SafeReturnUrl(string? returnUrl, string pathBase = "") =>
        returnUrl is { Length: > 0 and <= MaxReturnUrlLength }
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.Any(c => char.IsControl(c) || c == '\\')
            ? returnUrl
            : $"{pathBase}/admin";
}
