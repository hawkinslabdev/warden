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

    /// <summary>local path length cap; blocks cookie bloat.</summary>
    internal const int MaxReturnUrlLength = 512;

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app, AuthOptions options)
    {
        app.MapGet($"{options.AuthPath}/login", (HttpContext ctx, string? returnUrl) =>
        {
            // single-use cookie; never cache.
            ctx.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, private";
            ctx.Response.Headers.Pragma = "no-cache";
            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = SafeReturnUrl(returnUrl, ctx.Request.PathBase, options.AdminPath) },
                [OpenIdConnectDefaults.AuthenticationScheme]);
        }).RequireRateLimiting(RateLimitPolicies.Auth);

        // post only; validated by hand, no bound form.
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

    // blocks open redirect; rejects control chars.
    internal static string SafeReturnUrl(string? returnUrl, string pathBase = "", string adminPath = "/admin") =>
        returnUrl is { Length: > 0 and <= MaxReturnUrlLength }
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.Any(c => char.IsControl(c) || c == '\\')
            ? returnUrl
            : $"{pathBase}{adminPath}";
}
