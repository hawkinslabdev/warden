using System.Collections.Concurrent;
using System.Security.Cryptography;
using Ixnas.AltchaNet;
using Microsoft.AspNetCore.DataProtection;
using Warden.Models;
using Warden.Services;
using Warden.Services.Layout;
using Warden.Services.Rendering;
using Warden.Services.Theming;

namespace Warden.Configuration;

public static class AltchaGate
{
    internal const string CookieName = "warden_altcha";
    private const string ProtectorPurpose = "Warden.Altcha.Session";
    internal static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(24);

    public static IServiceCollection AddAltchaGate(this IServiceCollection services)
    {
        services.AddDataProtection();

        var key = new byte[64];
        RandomNumberGenerator.Fill(key);
        var store = new AltchaInMemoryStore();

        services.AddSingleton(_ => Altcha.CreateService(new AltchaSha256Configuration
        {
            Key = AltchaKey.FromBytes(key),
            StoreFactory = () => store,
        }));
        return services;
    }

    public static IApplicationBuilder UseAltchaGate(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments("/altcha") || path.StartsWithSegments("/health") || path.StartsWithSegments("/api"))
            {
                await next(context);
                return;
            }

            if (context.Request.Cookies.TryGetValue(CookieName, out var token) && IsValidSession(context, token))
            {
                await next(context);
                return;
            }

            await RenderChallengePage(context);
        });

    private static bool IsValidSession(HttpContext context, string token)
    {
        try
        {
            GetProtector(context).Unprotect(token, out _);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    internal static string IssueSessionCookieValue(HttpContext context) =>
        GetProtector(context).Protect("ok", SessionLifetime);

    private static ITimeLimitedDataProtector GetProtector(HttpContext context) =>
        context.RequestServices.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(ProtectorPurpose)
            .ToTimeLimitedDataProtector();

    private static Task RenderChallengePage(HttpContext context)
    {
        var themeOptions = context.RequestServices.GetRequiredService<ThemeOptions>();
        var settings = context.RequestServices.GetRequiredService<PageRequestSettings>();
        var content = context.RequestServices.GetRequiredService<ContentService>();
        var config = content.SiteConfig;
        var activeTheme = ThemeSelection.Resolve(themeOptions, settings.CliTheme, config?.Theme).Theme;
        var lang = Config.ResolveLocale(config)?.Code ?? "en";

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.ContentSecurityPolicy = context.Response.Headers.ContentSecurityPolicy + "; worker-src 'self' blob:";
        return context.Response.WriteAsync(BuildChallengePageHtml(activeTheme, lang));
    }

    private static string BuildChallengePageHtml(IWardenTheme activeTheme, string lang)
    {
        var l = Localization.Current;
        var lightVars = ThemeCssBuilder.BuildMinimalLightTokenCss(activeTheme)
            + "            --gate-error: #cf222e;\n";
        var darkVars = ThemeCssBuilder.BuildMinimalTokenCss(activeTheme)
            + ";--gate-error:#f85149;";
        var darkCss = "@media (prefers-color-scheme: dark) {"
            + ":root:not([data-theme=\"light\"]) {" + darkVars + "}"
            + "}"
            + ":root[data-theme=\"dark\"] {" + darkVars + "}";

        return $$"""
            <!doctype html>
            <html lang="{{LayoutProvider.HtmlEncode(lang)}}" class="theme-{{activeTheme.Name}}">
            <head>
            <meta charset="utf-8">
            {{ThemeInitScript}}
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="robots" content="noindex,nofollow">
            <title>{{LayoutProvider.HtmlEncode(l.AltchaGateTitle)}}</title>
            <script type="module" src="/js/altcha.min.js"></script>
            <style>
                :root {
                {{lightVars}}    --font-sans: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
                }
                {{darkCss}}
                * { box-sizing: border-box; margin: 0; padding: 0; }
                body {
                font-family: var(--font-sans);
                background-color: var(--bg-color);
                color: var(--text-color);
                display: flex;
                align-items: center;
                justify-content: center;
                min-height: 100vh;
                line-height: 1.6;
                }
                .gate { text-align: center; max-width: 22rem; padding: 0 1.5rem; }
                .gate-bar { width: 8rem; height: 3px; margin: 0 auto 1.5rem; border-radius: 3px; background: color-mix(in srgb, var(--accent) 18%, transparent); overflow: hidden; position: relative; }
                .gate-bar::after { content: ""; position: absolute; inset: 0; width: 40%; border-radius: 3px; background: var(--accent); animation: warden-altcha-scan 1.1s ease-in-out infinite; }
                @keyframes warden-altcha-scan { 0% { transform: translateX(-100%); } 100% { transform: translateX(350%); } }
                @media (prefers-reduced-motion: reduce) { .gate-bar::after { animation: none; width: 100%; transform: none; } }
                .gate h1 { font-size: 1.3rem; font-weight: 600; letter-spacing: -0.02em; margin-bottom: .5rem; }
                .gate p { color: var(--text-muted); }
                .gate-error { display: none; }
                .gate.is-error .gate-bar, .gate.is-error .gate-progress { display: none; }
                .gate.is-error .gate-error { display: block; }
                .gate.is-error h1 { color: var(--gate-error); }
            </style>
            </head>
            <body>
            <main id="gate" class="gate" role="status" aria-live="polite" aria-atomic="true">
              <div class="gate-bar" aria-hidden="true"></div>
              <div class="gate-progress">
                <h1>{{LayoutProvider.HtmlEncode(l.AltchaGateTitle)}}</h1>
                <p>{{LayoutProvider.HtmlEncode(l.AltchaGateDetail)}}</p>
              </div>
              <div class="gate-error">
                <h1>{{LayoutProvider.HtmlEncode(l.AltchaGateErrorTitle)}}</h1>
                <p>{{LayoutProvider.HtmlEncode(l.AltchaGateErrorDetail)}}</p>
              </div>
            </main>
            <altcha-widget challenge="/altcha/challenge" auto="onload" display="invisible" configuration='{"minDuration":800}'></altcha-widget>
            <noscript><style>#gate { display: none; }</style><p class="gate">{{LayoutProvider.HtmlEncode(l.AltchaGateNoScript)}}</p></noscript>
            <script>
                (function () {
                "use strict";
                var gate = document.getElementById("gate");

                function fail() {
                    gate.classList.add("is-error");
                }

                document.querySelector("altcha-widget").addEventListener("statechange", function (ev) {
                    var state = ev.detail.state;
                    if (state === "error") { fail(); return; }
                    if (state !== "verified") return;

                    var fd = new FormData();
                    fd.set("altcha", ev.detail.payload);
                    fetch("/altcha/verify", { method: "POST", body: fd, credentials: "same-origin" })
                    .then(function (r) { if (r.ok) { location.reload(); } else { fail(); } })
                    .catch(fail);
                });
                })();
            </script>
            </body></html>
            """;
    }

    private const string ThemeInitScript = "<script>(function(){"
        + "function apply(){try{var t=localStorage.getItem('warden-theme');var r=document.documentElement;"
        + "if(t==='dark'||t==='light'){r.setAttribute('data-theme',t);r.style.colorScheme=t;}"
        + "else{r.removeAttribute('data-theme');r.style.colorScheme='';}"
        + "}catch(e){}}"
        + "apply();"
        + "window.addEventListener('pageshow',function(e){if(e.persisted)apply();});"
        + "})()</script>";
}

internal sealed class AltchaInMemoryStore : IAltchaChallengeStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _stored = new();

    public Task Store(string challenge, DateTimeOffset expiryUtc)
    {
        _stored[challenge] = expiryUtc;
        return Task.CompletedTask;
    }

    public Task<bool> Exists(string challenge)
    {
        if (!_stored.TryGetValue(challenge, out var expiryUtc))
            return Task.FromResult(false);

        if (expiryUtc <= DateTimeOffset.UtcNow)
        {
            _stored.TryRemove(challenge, out _);
            return Task.FromResult(false);
        }

        return Task.FromResult(true);
    }
}
