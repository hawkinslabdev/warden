using Warden.Services.Rendering;

namespace Warden.Services;

public sealed record PageRequestSettings(
    string BasePath,
    string? CustomCsp,
    string? AutoCustomCssUrl,
    string? AutoCustomJsUrl,
    string WebRootPath,
    string DocsRootAbsolute,
    string? PublicBaseUrl,
    string? CliTheme = null,
    string? CliStructure = null)
{
    /// <summary>Blank means absent. An empty setting must not count as "configured" and mask a later source.</summary>
    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('/');

    /// <summary>Precedence: <c>--base-url</c>, then <c>Docs:PublicBaseUrl</c>, then the bare <c>PublicBaseUrl</c> alias.</summary>
    public static string? ResolvePublicBaseUrl(string? cliBaseUrl, string? docsOption, string? alias) =>
        Normalize(cliBaseUrl) ?? Normalize(docsOption) ?? Normalize(alias);

    /// <summary>
    /// Absolute origin for canonical URLs, feeds and sitemaps; <c>PublicBaseUrl</c> wins, as the Host header is caller-supplied.
    /// </summary>
    public string Origin(HttpContext context) =>
        Normalize(PublicBaseUrl) ?? $"{context.Request.Scheme}://{context.Request.Host}";
}

public sealed class PageRequestHandler
{
    private readonly ContentService _content;
    private readonly PageResponder _responder;

    public PageRequestHandler(ContentService content, PageResponder responder)
    {
        _content = content;
        _responder = responder;
    }

    public async Task HandleAsync(string? path, HttpContext context)
    {
        var normalized = (path ?? string.Empty).Trim('/').ToLowerInvariant();

        // "/" itself is claimed by StatusEndpoints (exact routes outrank this catch-all), so an empty
        // path here only reaches this handler if that route somehow didn't match; treat it as a 404.
        var page = normalized.Length == 0
            ? await _content.GetPageAsync("", context.RequestAborted)
            : await _content.GetPageAsync($"pages/{normalized}", context.RequestAborted)
                ?? await _content.GetPageAsync(normalized, context.RequestAborted);

        if (page is null)
        {
            await _responder.Write404Async(context);
            return;
        }

        if (page.Redirect is { Length: > 0 } target
            && TryResolveRedirect(target, _responder.BasePath, context, _content.SiteConfig, out var resolved))
        {
            context.Response.Redirect(resolved, permanent: false);
            return;
        }

        var (incidentBack, incidentChrome) = BuildIncidentChrome(page, _responder.BasePath);
        var header = $"<header class=\"page-header\"><h1 class=\"page-title\">{Layout.LayoutProvider.HtmlEncode(page.Title)}</h1></header>";
        var cover = PageChromeRenderer.BuildCover(page.Cover, _responder.BasePath);
        var updated = BuildUpdatedStamp(page);
        var pageNav = await BuildPageNav(page, context.RequestAborted);

        await _responder.WriteAsync(context, new PageView(
            Title: page.Title,
            ContentHtml: incidentBack + header + incidentChrome + cover + page.HtmlContent + updated + pageNav,
            Description: page.Description,
            CanonicalPath: normalized,
            IsArticle: true,
            Prose: true,
            Image: page.Cover,
            Modified: page.Updated ?? page.LastModified,
            NoIndex: page.NoIndex || (_content.SiteConfig?.NoIndex?.Pages ?? false)));
    }

    // content/incidents/*.md pages get a status badge and a Started/Resolved (or Started/Ends) row, matching the meta a status page reader expects up front.
    private static (string Back, string Chrome) BuildIncidentChrome(Models.DocumentationPage page, string basePath)
    {
        if (page.OriginalRelativePath?.StartsWith("incidents/", StringComparison.Ordinal) != true)
            return (string.Empty, string.Empty);

        var l = Localization.Current;
        var back = $"<p class=\"status-detail-back\"><a href=\"{UrlPaths.Href(basePath, "")}\">{Layout.LayoutProvider.HtmlEncode(l.StatusIncidentBack)}</a></p>";

        var start = IncidentContent.StartOf(page);
        var meta = new System.Text.StringBuilder();
        string badgeClass, badgeText;

        if (page.Maintenance)
        {
            var now = DateTimeOffset.UtcNow;
            badgeClass = IncidentContent.MaintenanceBadgeClass(page, now);
            badgeText = badgeClass switch
            {
                "active" => l.StatusMaintenanceActive,
                "ended" => l.StatusMaintenanceEnded,
                _ => l.StatusMaintenancePlanned,
            };
            AppendMeta(meta, l.StatusIncidentStarted, IncidentContent.TimeHtml(start));
            if (IncidentContent.EndOf(page) is { } end)
                AppendMeta(meta, l.StatusMaintenanceEnds, IncidentContent.TimeHtml(end));
        }
        else
        {
            var resolvedAt = IncidentContent.EndOf(page);
            badgeClass = IncidentContent.IncidentBadgeClass(page);
            badgeText = resolvedAt is null ? l.StatusDown : l.StatusResolved;
            AppendMeta(meta, l.StatusIncidentStarted, IncidentContent.TimeHtml(start));
            if (resolvedAt is { } end)
                AppendMeta(meta, l.StatusIncidentEnded, IncidentContent.TimeHtml(end));
        }

        var badgeGroup = page.Maintenance ? "status-maintenance-badge" : "status-incident-badge";
        var chrome = $"<div class=\"status-detail-chrome\">" +
                     $"<span class=\"{badgeGroup} {badgeGroup}--{badgeClass}\">{Layout.LayoutProvider.HtmlEncode(badgeText)}</span>" +
                     $"<div class=\"status-detail-meta\">{meta}</div>" +
                     "</div>";
        return (back, chrome);
    }

    private static void AppendMeta(System.Text.StringBuilder meta, string label, string valueHtml) =>
        meta.Append("<span class=\"status-detail-meta-item\"><span class=\"status-detail-meta-label\">")
            .Append(Layout.LayoutProvider.HtmlEncode(label))
            .Append("</span>").Append(valueHtml).Append("</span>");

    // Opt-in: only pages with an explicit updated:/date: front matter show the stamp (never file mtime).
    private static string BuildUpdatedStamp(Models.DocumentationPage page)
    {
        if (!page.ShowLastUpdated || page.Updated is not { } when)
            return string.Empty;
        var human = DateFormatter.Current.Medium(when);
        var label = Layout.LayoutProvider.HtmlEncode(Localization.Current.LastUpdated);
        return $"<p class=\"page-updated\">{label} <time datetime=\"{DateFormatter.Iso(when)}\">{human}</time></p>";
    }

    private async Task<string> BuildPageNav(Models.DocumentationPage page, CancellationToken ct)
    {
        if (!page.ShowPagination || (page.PagePrev is null && page.PageNext is null))
            return string.Empty;

        var (prevHref, prevTitle) = await ResolvePageLink(page.PagePrev, ct);
        var (nextHref, nextTitle) = await ResolvePageLink(page.PageNext, ct);
        return PageChromeRenderer.BuildAdjacentNav(prevHref, prevTitle, nextHref, nextTitle, Localization.Current.PageNavAria);
    }

    private async ValueTask<(string? Href, string? Title)> ResolvePageLink(string? target, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(target)) return (null, null);
        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return (target, target);

        var norm = target.Trim('/').ToLowerInvariant();
        var targetPage = await _content.GetPageAsync($"pages/{norm}", ct) ?? await _content.GetPageAsync(norm, ct);
        return (UrlPaths.Href(_responder.BasePath, norm), targetPage?.Title ?? norm);
    }

    /// <summary>False when an absolute target's host is neither this site's nor listed in <c>config.json</c>'s <c>redirectHosts</c>; the caller then renders the page instead of forwarding.</summary>
    internal static bool TryResolveRedirect(
        string target, string basePath, HttpContext context, Models.Config? config, out string resolved)
    {
        resolved = string.Empty;
        var isAbsolute = target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (isAbsolute && !IsAllowedRedirectHost(target, context.Request.Host.Host, config?.RedirectHosts))
        {
            Serilog.Log.Warning(
                "Redirect to {Target} is not allowed: its host is not listed in config.json redirectHosts; the page rendered instead",
                target);
            return false;
        }

        resolved = ResolveRedirect(target, basePath);
        return true;
    }

    internal static bool IsAllowedRedirectHost(string target, string requestHost, IReadOnlyList<string>? allowedHosts)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
            return false;

        if (uri.Host.Equals(requestHost, StringComparison.OrdinalIgnoreCase))
            return true;

        return allowedHosts is { Count: > 0 }
            && allowedHosts.Any(h => uri.Host.Equals(h.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    internal static string ResolveRedirect(string target, string basePath)
    {
        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return target;

        var trimmed = target.Trim('/');
        return trimmed.Length == 0
            ? (basePath.Length == 0 ? "/" : $"{basePath}/")
            : (basePath.Length == 0 ? $"/{trimmed}/" : $"{basePath}/{trimmed}/");
    }
}
