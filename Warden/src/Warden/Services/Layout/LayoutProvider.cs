using System.Text;
using Warden.Models;
using Warden.Services.Rendering;
using Warden.Services.Theming;

namespace Warden.Services.Layout;

public static partial class LayoutProvider
{
    public static string GetLayout(
        string title,
        string content,
        string? themeCss = null,
        string? brandText = null,
        string? brandImage = null,
        ThemeMode themeMode = ThemeMode.Auto,
        bool enableLiveReload = false,
        long buildVersion = 0,
        string? favicon = null,
        string? description = null,
        bool noIndex = false,
        bool isHomePage = false,
        bool showScrollIndicator = true,
        string basePath = "",
        string lang = "en",
        string? headTagsHtml = null,
        string? keywordsHtml = null,
        string? canonicalUrl = null,
        string? nonce = null,
        bool hasMath = false,
        bool hasMermaid = false,
        bool hasMap = false,
        string? promoBarHtml = null,
        bool isArticle = false,
        bool proseStyle = false,
        string? siteNavHtml = null,
        string? footerHtml = null,
        string? socialLinksHtml = null,
        string? pageId = null,
        string? customAssetsHtml = null,
        string? socialMetaHtml = null,
        string? structuredDataHtml = null,
        IWardenTheme? theme = null,
        IWardenStructure? structure = null)
    {
        var l = Localization.Current;
        var scrollIndicatorHtml = showScrollIndicator ? @"<div id=""scroll-indicator""></div>" : "";
        var shareOverlayHtml = isArticle ? BuildShareOverlay() : "";
        var faviconHtml = BuildFaviconLink(favicon, basePath);
        var homeHref = basePath.Length == 0 ? "/" : $"{basePath}/";
        var brandMarkHtml = BuildBrandMark(brandImage, basePath);
        var descriptionHtml = !string.IsNullOrWhiteSpace(description)
            ? $"<meta name=\"description\" content=\"{HtmlEncode(description)}\">"
            : "";
        var robotsHtml = noIndex ? "<meta name=\"robots\" content=\"noindex, follow\">" : "";

        var contentClass = proseStyle ? "content reading prose" : "content";
        var dataPageAttr = string.IsNullOrEmpty(pageId) ? "" : $" data-page=\"{HtmlEncode(pageId)}\"";

        var activeTheme = theme ?? ThemeRegistry.Default;
        var activeStructure = structure ?? StructureRegistry.Default;
        var themeTokenCss = ThemeCssBuilder.BuildTokenCss(activeTheme, themeMode);
        // Structure CSS last, so a page shape choice always wins over a theme's own smaller touches.
        var componentCss = activeStructure.ComponentCss.Length > 0
            ? activeTheme.ComponentCss + "\n" + activeStructure.ComponentCss
            : activeTheme.ComponentCss;

        var enableDarkMode = themeMode == ThemeMode.Auto;

        var forcedThemeAttr = themeMode switch
        {
            ThemeMode.Dark => " data-theme=\"dark\"",
            ThemeMode.Light => " data-theme=\"light\"",
            _ => "",
        };

        var nonceAttr = nonce is { Length: > 0 } ? $" nonce=\"{nonce}\"" : "";
        // Pre-<style> so no transition can fire; without a stored theme, data-theme stays unset so CSS follows live OS changes.
        var themeInitScript = enableDarkMode
            ? "<script" + nonceAttr + ">(function(){try{var t=localStorage.getItem('warden-theme');if(t==='dark'||t==='light'){var r=document.documentElement;r.setAttribute('data-theme',t);r.style.colorScheme=t;}}catch(e){}})();</script>"
            : "";

        var themeToggleHtml = enableDarkMode
            ? $@"<button type=""button"" class=""icon-btn theme-toggle"" id=""theme-toggle"" role=""switch"" aria-checked=""false"" aria-label=""{HtmlEncode(l.ThemeToggle)}"">
                <svg class=""icon-sun"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><circle cx=""12"" cy=""12"" r=""4""/><path d=""M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M4.93 19.07l1.41-1.41M17.66 6.34l1.41-1.41""/></svg>
                <svg class=""icon-moon"" viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M21 12.79A9 9 0 1111.21 3 7 7 0 0021 12.79z""/></svg>
            </button>"
            : "";

        var colorSchemeMeta = themeMode switch
        {
            ThemeMode.Auto => "<meta name=\"color-scheme\" content=\"light dark\">",
            ThemeMode.Dark => "<meta name=\"color-scheme\" content=\"dark\">",
            _ => "<meta name=\"color-scheme\" content=\"light\">",
        };

        var canonicalLink = canonicalUrl is { Length: > 0 }
            ? $"<link rel=\"canonical\" href=\"{HtmlEncode(canonicalUrl)}\">"
            : string.Empty;

        return $@"
<!DOCTYPE html>
<html lang=""{HtmlEncode(lang)}"" class=""theme-{HtmlEncode(activeTheme.Name)}""{forcedThemeAttr}>
<head>
    <meta charset=""UTF-8"">
    {themeInitScript}
    {colorSchemeMeta}
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{HtmlEncode(title)}</title>
    {descriptionHtml}
    {robotsHtml}
    {keywordsHtml}
    {canonicalLink}
    {socialMetaHtml}
    {structuredDataHtml}
    {faviconHtml}
    {headTagsHtml}
    {GetStyles(themeTokenCss, componentCss, basePath, nonce)}
    {themeCss}
    {customAssetsHtml}
    {(hasMath ? $"<link rel=\"stylesheet\" href=\"{basePath}/css/katex.min.css\">" : "")}
    {(hasMermaid ? $"<script defer src=\"{basePath}/js/mermaid.min.js\"></script>" : "")}
    {(hasMap ? $"<link rel=\"stylesheet\" href=\"{basePath}/css/leaflet.css\"><script defer src=\"{basePath}/js/leaflet.js\"></script>" : "")}
</head>
<body>
    <a href=""#main-content"" class=""skip-link"">{HtmlEncode(l.SkipToContent)}</a>
    {promoBarHtml}
    {scrollIndicatorHtml}
    <header class=""topbar"">
        <div class=""masthead-actions"">
            {socialLinksHtml}
            {themeToggleHtml}
        </div>
        <a class=""brand"" href=""{homeHref}"">{brandMarkHtml}{brandText ?? "Warden"}</a>
        {siteNavHtml}
    </header>
    {shareOverlayHtml}
    <main class=""main-container"" id=""main-content"" tabindex=""-1"">
        <article class=""{contentClass}""{dataPageAttr}>
            {content}
        </article>
    </main>
    {footerHtml}
    {GetScripts(enableLiveReload, enableDarkMode, buildVersion, basePath, nonce)}
</body>
</html>";
    }

    public static string Get404Layout(
        Func<string?, string> htmlEncode,
        string basePath = "",
        string lang = "en",
        IWardenTheme? theme = null)
    {
        var l = Localization.Current;
        var homeHref = basePath.Length == 0 ? "/" : $"{basePath}/";
        var activeTheme = theme ?? ThemeRegistry.Default;
        // Build outside the interpolated block so JS/CSS braces don't need escaping.
        var darkVars = ThemeCssBuilder.BuildMinimalTokenCss(activeTheme);
        var lightVars = ThemeCssBuilder.BuildMinimalLightTokenCss(activeTheme);
        const string themeInit = "<script>(function(){" +
            "function apply(){try{var t=localStorage.getItem('warden-theme');var r=document.documentElement;" +
            "if(t==='dark'||t==='light'){r.setAttribute('data-theme',t);r.style.colorScheme=t;}" +
            "else{r.removeAttribute('data-theme');r.style.colorScheme='';}" +
            "}catch(e){}}" +
            "apply();" +
            "window.addEventListener('pageshow',function(e){if(e.persisted)apply();});" +
            "})()</script>";
        var darkCss = "@media (prefers-color-scheme: dark) {" +
            ":root:not([data-theme=\"light\"]) {" + darkVars + "}" +
            "}" +
            ":root[data-theme=\"dark\"] {" + darkVars + "}";
        return $@"
<!DOCTYPE html>
<html lang=""{htmlEncode(lang)}"" class=""theme-{htmlEncode(activeTheme.Name)}"">
<head>
    <meta charset=""UTF-8"">
    {themeInit}
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{htmlEncode(l.NotFoundTitle)}</title>
    <style>
        :root {{
{lightVars}            --font-sans: system-ui, -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, sans-serif;
        }}
        {darkCss}
        * {{ box-sizing: border-box; margin: 0; padding: 0; }}
        body {{
            font-family: var(--font-sans);
            background-color: var(--bg-color);
            color: var(--text-color);
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            line-height: 1.6;
        }}
        .select-none {{
            -webkit-touch-callout: none;
            -webkit-user-select: none;
            -khtml-user-select: none;
            -moz-user-select: none;
            -ms-user-select: none;
            user-select: none;
        }}
        .not-found {{
            text-align: center;
        }}
        .not-found h1 {{
            font-size: 4rem;
            font-weight: 600;
            letter-spacing: -0.03em;
            margin-bottom: 0.5rem;
        }}
        .not-found p {{
            color: var(--text-muted);
            margin-bottom: 2rem;
        }}
        .not-found a {{
            color: var(--accent);
            text-decoration: none;
            font-weight: 500;
        }}
        .not-found a:hover {{
            text-decoration: underline;
        }}
    </style>
</head>
<body>
    <div class=""not-found"">
        <h1>404</h1>
        <p>{htmlEncode(l.NotFoundMessage)}</p>
        <a href=""{homeHref}"">{htmlEncode(l.NotFoundHome)}</a>
    </div>
</body>
</html>";
    }

    public static string HtmlEncode(string? value) =>
        value != null ? System.Net.WebUtility.HtmlEncode(value) : string.Empty;

    public static string? ResolveAssetUrl(string? url, string basePath)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;

        if (url.StartsWith('/'))
        {
            if (HasBasePathPrefix(url, basePath))
                return url;
            return $"{basePath}{url}";
        }

        return url;
    }

    private static bool HasBasePathPrefix(string path, string basePath)
    {
        if (string.IsNullOrEmpty(basePath))
            return false;
        return path.StartsWith(basePath, StringComparison.Ordinal)
            && (path.Length == basePath.Length || path[basePath.Length] == '/');
    }

    private static string BuildShareOverlay()
    {
        var l = Localization.Current;
        return $@"
    <div class=""share-overlay"" id=""share-overlay"" hidden>
        <div class=""share-modal"" role=""dialog"" aria-modal=""true"" aria-labelledby=""share-modal-label"">
            <button type=""button"" class=""share-modal-close icon-btn"" id=""share-modal-close"" aria-label=""{HtmlEncode(l.ShareTitle)}"">
                <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" aria-hidden=""true""><path d=""M18 6L6 18M6 6l12 12""/></svg>
            </button>
            <h2 id=""share-modal-label"" class=""share-modal-title"">{HtmlEncode(l.ShareTitle)}</h2>
            <div class=""share-actions"">
                <button type=""button"" class=""share-action"" id=""share-copy"">
                    <span class=""share-action-icon""><svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71""/><path d=""M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71""/></svg></span>
                    <span class=""share-action-label"" id=""share-copy-label"">{HtmlEncode(l.ShareCopy)}</span>
                </button>
                <button type=""button"" class=""share-action"" id=""share-mastodon"">
                    <span class=""share-action-icon""><svg viewBox=""0 0 24 24"" fill=""currentColor"" aria-hidden=""true""><path d=""M21.6 13.9c-.3 1.5-2.6 3.2-5.3 3.5-1.4.2-2.8.3-4.2.2-2.3-.1-4.1-.5-4.1-.5v.6c.3 2.2 2.2 2.4 4 2.4 1.8.1 3.4-.5 3.4-.5l.1 1.6s-1.3.7-3.5.8c-1.3.1-2.8-.1-4.6-.5-3.7-1-4.4-5-4.4-9V6.9c0-4 2.6-5.2 2.6-5.2C5.6.9 8 .8 10.4.8h.1c2.5 0 4.8.1 6.2.7 0 0 2.6 1.2 2.6 5.2 0 0 .1 2.9-.3 4.4l1.6.6-.1.7ZM17.9 7c0-1.3-.4-2-1.1-2.6-.7-.6-1.7-.9-2.8-.9-1.3 0-2.3.5-2.9 1.5l-.6.9-.6-.9c-.6-1-1.6-1.5-2.9-1.5-1.1 0-2.1.3-2.8.9-.7.6-1.1 1.3-1.1 2.6v5.5h2.2V7.2c0-1.3.5-1.9 1.6-1.9 1.2 0 1.8.8 1.8 2.3v3.4h2.2V7.6c0-1.5.6-2.3 1.8-2.3 1.1 0 1.6.6 1.6 1.9v5.3h2.2V7Z""/></svg></span>
                    <span class=""share-action-label"">Mastodon</span>
                </button>
                <a class=""share-action"" id=""share-linkedin"" target=""_blank"" rel=""noopener noreferrer"">
                    <span class=""share-action-icon""><svg viewBox=""0 0 24 24"" fill=""currentColor"" aria-hidden=""true""><path d=""M6.94 5a1.94 1.94 0 1 1-3.88 0 1.94 1.94 0 0 1 3.88 0ZM3.3 8.5h3.4V21H3.3V8.5Zm5.5 0h3.26v1.7h.05c.45-.86 1.56-1.77 3.2-1.77 3.42 0 4.05 2.25 4.05 5.18V21h-3.4v-5.5c0-1.3-.02-3-1.83-3-1.83 0-2.11 1.43-2.11 2.9V21H8.8V8.5Z""/></svg></span>
                    <span class=""share-action-label"">LinkedIn</span>
                </a>
                <a class=""share-action"" id=""share-email"">
                    <span class=""share-action-icon""><svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><path d=""m22 7-8.99 5.73a2 2 0 0 1-2.02 0L2 7""/><rect x=""2"" y=""4"" width=""20"" height=""16"" rx=""2""/></svg></span>
                    <span class=""share-action-label"">Email</span>
                </a>
            </div>
        </div>
    </div>";
    }

    private static string GetNonceAttr(string? nonce) =>
        nonce is { Length: > 0 } ? $" nonce=\"{nonce}\"" : string.Empty;

    private static string BuildBrandMark(string? brandImage, string basePath)
    {
        // Undeclared keeps the default mark; declared-but-blank means "show no mark at all".
        if (brandImage is null)
            return "<span class=\"brand-mark\" aria-hidden=\"true\">\U0001F375</span>";
        if (string.IsNullOrWhiteSpace(brandImage))
            return string.Empty;

        // Emoji or plain text mark; not a URL or path
        if (!brandImage.StartsWith('/')
            && !brandImage.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !brandImage.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !brandImage.Contains('/')
            && !brandImage.Contains('.'))
            return $"<span class=\"brand-mark\" aria-hidden=\"true\">{HtmlEncode(brandImage)}</span>";

        return $"<img src=\"{HtmlEncode(ResolveAssetUrl(brandImage, basePath))}\" alt=\"\">";
    }

    private static string BuildFaviconLink(string? favicon, string basePath = "")
    {
        if (string.IsNullOrWhiteSpace(favicon))
            return string.Empty;

        // Emoji or plain text fallback; not a URL or path
        if (!favicon.StartsWith('/')
            && !favicon.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !favicon.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var svg = $"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'><text y='.9em' font-size='90'>{favicon}</text></svg>";
            var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
            return $"<link rel=\"icon\" href=\"data:image/svg+xml;base64,{base64}\">";
        }

        var href = ResolveAssetUrl(favicon, basePath);
        return $"<link rel=\"icon\" href=\"{HtmlEncode(href)}\">";
    }
}
