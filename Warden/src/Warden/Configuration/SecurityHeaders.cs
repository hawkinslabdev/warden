using Microsoft.AspNetCore.Builder;

namespace Warden.Configuration;

public static class SecurityHeaders
{
    public const string DefaultCsp =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "style-src-attr 'unsafe-inline'; " +
        "img-src 'self' data: https://tile.openstreetmap.org https://*.tile.openstreetmap.org; " +
        "font-src 'self' data:; " +
        "connect-src 'self'; " +
        // No default-src fallback for these as far as I know  
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "object-src 'none'; " +
        "frame-ancestors 'none'";

    public static Task Apply(HttpContext context, Func<Task> next) =>
        Apply(context, next, DefaultCsp);

    public static Task Apply(HttpContext context, Func<Task> next, string contentSecurityPolicy)
    {
        context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Response.Headers.XFrameOptions = "DENY";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers.ContentSecurityPolicy = contentSecurityPolicy;
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
        context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
        // same-site, not same-origin: post images and fonts are routinely embedded from a sibling host.
        context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-site";
        context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";

        if (context.Request.IsHttps)
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        return next();
    }

    /// <param name="nonceStyles">False keeps <c>style-src 'unsafe-inline'</c>, for pages whose scripts inject unnonced style elements. Scripts stay nonced.</param>
    public static string BuildNonceCsp(string baseCsp, string nonce, bool nonceStyles = true)
    {
        var noncePart = $"'nonce-{nonce}'";
        var directives = baseCsp.Split(';');
        for (var i = 0; i < directives.Length; i++)
        {
            var trimmed = directives[i].TrimStart();
            if (!nonceStyles && trimmed.StartsWith("style-src "))
                continue;

            if (!trimmed.StartsWith("script-src ") && !trimmed.StartsWith("style-src "))
                continue;

            // A hardened policy has no 'unsafe-inline' to swap, but still needs the nonce or its own inline scripts break.
            directives[i] = trimmed.Contains("'unsafe-inline'")
                ? directives[i].Replace("'unsafe-inline'", noncePart)
                : directives[i].TrimEnd() + " " + noncePart;
        }
        return string.Join(";", directives);
    }
}

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, string? contentSecurityPolicy = null)
    {
        var csp = contentSecurityPolicy ?? SecurityHeaders.DefaultCsp;
        return app.Use((context, next) => SecurityHeaders.Apply(context, next, csp));
    }
}
