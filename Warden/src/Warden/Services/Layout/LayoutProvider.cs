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
        bool proseStyle = false,
        string? siteNavHtml = null,
        string? footerHtml = null,
        string? socialLinksHtml = null,
        string? pageId = null,
        string? customAssetsHtml = null,
        string? socialMetaHtml = null,
        string? structuredDataHtml = null,
        IWardenTheme? theme = null,
        string? authButtonHtml = null)
    {
        var l = Localization.Current;
        var scrollIndicatorHtml = showScrollIndicator ? @"<div id=""scroll-indicator""></div>" : "";
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
        var themeTokenCss = ThemeCssBuilder.BuildTokenCss(activeTheme, themeMode);
        var componentCss = activeTheme.ComponentCss;

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

        var timezoneWidgetHtml = $@"<div class=""timezone-widget"">
                <button type=""button"" class=""icon-btn timezone-toggle"" id=""timezone-toggle"" aria-haspopup=""true"" aria-expanded=""false"" aria-label=""{HtmlEncode(l.StatusTimezoneAriaLabel)}"">
                    <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true""><circle cx=""12"" cy=""12"" r=""10""/><path d=""M12 6v6l4 2""/></svg>
                </button>
                <div class=""timezone-dropdown"" id=""timezone-dropdown"" hidden>
                    <input type=""text"" class=""timezone-search"" id=""timezone-search"" placeholder=""{HtmlEncode(l.StatusTimezoneSearchPlaceholder)}"" autocomplete=""off"" spellcheck=""false"" aria-label=""{HtmlEncode(l.StatusTimezoneSearchPlaceholder)}"">
                    <div class=""timezone-options"" id=""timezone-options""></div>
                </div>
            </div>";

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
        canonicalLink += $"<link rel=\"alternate\" type=\"application/atom+xml\" title=\"Incidents\" href=\"{basePath}/incidents/feed.xml\">";

        return CollapseBlankLines($@"
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
    {GetStylesLink(themeTokenCss, componentCss, basePath)}
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
            {authButtonHtml}
            {timezoneWidgetHtml}
            {themeToggleHtml}
        </div>
        <a class=""brand"" href=""{homeHref}"">{brandMarkHtml}{brandText ?? "Warden"}</a>
        {siteNavHtml}
    </header>
    <main class=""main-container"" id=""main-content"" tabindex=""-1"">
        <article class=""{contentClass}""{dataPageAttr}>
            {content}
        </article>
    </main>
    {footerHtml}
    {GetScriptsTag(enableLiveReload, enableDarkMode, buildVersion, basePath)}
</body>
</html>");
    }

    public static string Get404Layout(
        Func<string?, string> htmlEncode,
        string basePath = "",
        string lang = "en",
        IWardenTheme? theme = null)
    {
        var l = Localization.Current;
        return BuildMinimalStatusPage(htmlEncode, basePath, lang, theme, "404", l.NotFoundTitle, l.NotFoundMessage, l.NotFoundHome);
    }

    // no exception detail shown to visitors.
    public static string Get500Layout(
        Func<string?, string> htmlEncode,
        string basePath = "",
        string lang = "en",
        IWardenTheme? theme = null)
    {
        var l = Localization.Current;
        return BuildMinimalStatusPage(htmlEncode, basePath, lang, theme, "500", l.ServerErrorTitle, l.ServerErrorMessage, l.NotFoundHome);
    }

    private static string BuildMinimalStatusPage(
        Func<string?, string> htmlEncode, string basePath, string lang, IWardenTheme? theme,
        string code, string title, string message, string homeLabel)
    {
        var homeHref = basePath.Length == 0 ? "/" : $"{basePath}/";
        var activeTheme = theme ?? ThemeRegistry.Default;
        // avoids escaping js/css braces below.
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
    <title>{htmlEncode(title)}</title>
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
        <h1>{htmlEncode(code)}</h1>
        <p>{htmlEncode(message)}</p>
        <a href=""{homeHref}"">{htmlEncode(homeLabel)}</a>
    </div>
</body>
</html>";
    }

    public static string HtmlEncode(string? value) =>
        value != null ? System.Net.WebUtility.HtmlEncode(value) : string.Empty;

    private static string CollapseBlankLines(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return html;
        }

        var parts = System.Text.RegularExpressions.Regex.Split(
            html,
            @"(?i)(<article\b[^>]*>.*?</article>)",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        for (var i = 0; i < parts.Length; i += 2)
        {
            parts[i] = CollapseBlankLinesRegex(parts[i]);
        }

        return string.Concat(parts);
    }

    private static string CollapseBlankLinesRegex(string input) =>
        System.Text.RegularExpressions.Regex.Replace(input, @"(?m)^[ \t]*\r?\n", string.Empty);

    internal sealed record GeneratedAsset(string Key, string Body, string Version);

    // one mutable slot, not a dictionary keyed by build version, so long uptime with many hot-reloads can't leak
    private static GeneratedAsset GetOrBuildAsset(ref GeneratedAsset? slot, string key, Func<string> build)
    {
        var current = slot;
        if (current is not null && current.Key == key)
            return current;

        var body = build();
        var version = Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(body)))[..10];
        var next = new GeneratedAsset(key, body, version);
        slot = next;
        return next;
    }

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

    public static string BuildAuthButton(bool signedIn, string authPath, string adminPath, string basePath)
    {
        var l = Localization.Current;
        var prefix = basePath.TrimEnd('/');
        var label = HtmlEncode(signedIn ? l.AuthAdminPanel : l.AuthSignIn);
        var href = signedIn ? $"{prefix}{adminPath}" : $"{prefix}{authPath}/login";
        var icon = signedIn
            ? @"<line x1=""21"" x2=""14"" y1=""4"" y2=""4""/><line x1=""10"" x2=""3"" y1=""4"" y2=""4""/><line x1=""21"" x2=""12"" y1=""12"" y2=""12""/><line x1=""8"" x2=""3"" y1=""12"" y2=""12""/><line x1=""21"" x2=""16"" y1=""20"" y2=""20""/><line x1=""12"" x2=""3"" y1=""20"" y2=""20""/><line x1=""14"" x2=""14"" y1=""2"" y2=""6""/><line x1=""8"" x2=""8"" y1=""10"" y2=""14""/><line x1=""16"" x2=""16"" y1=""18"" y2=""22""/>"
            : @"<path d=""M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4""/><polyline points=""10 17 15 12 10 7""/><line x1=""15"" x2=""3"" y1=""12"" y2=""12""/>";

        return $@"<a class=""icon-btn auth-button{(signedIn ? " auth-button--active" : "")}"" href=""{HtmlEncode(href)}"" rel=""nofollow"" aria-label=""{label}"" data-tip=""{label}"">
                <svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"" aria-hidden=""true"">{icon}</svg>
            </a>";
    }

    private static string BuildBrandMark(string? brandImage, string basePath)
    {
        // Undeclared keeps the default mark; declared-but-blank means "show no mark at all".
        if (brandImage is null)
            return "<span class=\"brand-mark\" aria-hidden=\"true\">\U0001F375</span>";
        if (string.IsNullOrWhiteSpace(brandImage))
            return string.Empty;

        // Emoji or simple text mark; not a URL or path
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

        // Emoji or simple text fallback; not a URL or path
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
