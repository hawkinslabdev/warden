using System.Security.Cryptography;
using System.Text;
using Warden.Configuration;
using Warden.Models;
using Warden.Services.Layout;
using Warden.Services.Rendering;
using Warden.Services.Theming;

namespace Warden.Services;

public sealed record PageView(
    string Title,
    string ContentHtml,
    string? Description = null,
    string CanonicalPath = "",
    bool IsArticle = false,
    // drives the "reading prose" CSS wrapper only; independent of IsArticle's SEO/meta semantics (og:type, structured data)
    bool Prose = false,
    string? Image = null,
    DateTime? Modified = null,
    bool NoIndex = false);

public sealed class PageResponder
{
    private readonly ContentService _content;
    private readonly MarkdownService _markdown;
    private readonly ThemeOptions _theme;
    private readonly DocsOptions _docsOptions;
    private readonly PageRequestSettings _settings;
    private readonly string _iconsDir;
    private readonly string? _fallbackIconsDir;

    public PageResponder(
        ContentService content,
        MarkdownService markdown,
        ThemeOptions theme,
        DocsOptions docsOptions,
        PageRequestSettings settings)
    {
        _content = content;
        _markdown = markdown;
        _theme = theme;
        _docsOptions = docsOptions;
        _settings = settings;
        _iconsDir = Path.Combine(settings.WebRootPath, "icons");
        var defaultIconsDir = Path.Combine(AppContext.BaseDirectory, "wwwroot-default", "icons");
        _fallbackIconsDir = Directory.Exists(defaultIconsDir) ? defaultIconsDir : null;
    }

    public string BasePath => _settings.BasePath;
    public string HomeUrl => _settings.BasePath.Length == 0 ? "/" : $"{_settings.BasePath}/";

    private static string? ResolveSocialImage(string? image, string origin, string basePath)
    {
        if (string.IsNullOrWhiteSpace(image))
            return null;
        if (image.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || image.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return image;
        var path = image.StartsWith('/') ? image : "/" + image;
        return $"{origin}{basePath}{path}";
    }

    internal static string ComputeETag(string origin, long buildVersion, string html)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes($"{buildVersion}:{origin}:"));
        hash.AppendData(Encoding.UTF8.GetBytes(html));
        return Convert.ToBase64String(hash.GetHashAndReset()).TrimEnd('=');
    }

    /// <summary>Fresh per response, so it cannot be read off the public ETag. Rules out answering 304.</summary>
    private static string NewNonce() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

    // exposed so an endpoint can decide its own markup (e.g. StatusEndpoints' grouped-vs-flat layout)
    // before calling WriteAsync, using the exact same resolution WriteAsync uses internally
    public IWardenStructure ResolveStructure() =>
        StructureRegistry.Resolve(_settings.CliStructure ?? _content.SiteConfig?.Structure);

    public async Task WriteAsync(HttpContext context, PageView view)
    {
        var basePath = _settings.BasePath;
        var config = _content.SiteConfig;

        // Origin folded in so a shared cache cannot serve one host's body under another host's key.
        var pageOrigin = _settings.Origin(context);
        var etag = ComputeETag(pageOrigin, _content.BuildVersion, view.ContentHtml);
        context.Response.Headers.ETag = $"\"{etag}\"";
        context.Response.Headers.CacheControl = "no-cache";
        if (view.NoIndex)
            context.Response.Headers["X-Robots-Tag"] = "noindex, follow";
        if (_settings.PublicBaseUrl is null)
            context.Response.Headers.Vary = "Host";

        var nonce = NewNonce();
        var baseCsp = _settings.CustomCsp ?? SecurityHeaders.DefaultCsp;
        // Mermaid injects unnonced <style> elements, so only a diagram page relaxes style-src.
        var hasMermaid = view.ContentHtml.Contains("class=\"mermaid\"", StringComparison.Ordinal);
        context.Response.Headers.ContentSecurityPolicy =
            SecurityHeaders.BuildNonceCsp(baseCsp, nonce, nonceStyles: !hasMermaid);

        var themeSelection = ThemeSelection.Resolve(_theme, _settings.CliTheme, config?.Theme);
        var activeTheme = themeSelection.Theme;
        var activeStructure = StructureRegistry.Resolve(_settings.CliStructure ?? config?.Structure);
        var themeCss = ThemeProvider.BuildThemeCss(_theme, nonce);
        var customCssLink = ThemeProvider.BuildCustomCssLink(_theme, _settings.AutoCustomCssUrl, basePath);
        var customJsScript = ThemeProvider.BuildCustomJsScript(_theme, _settings.AutoCustomJsUrl, basePath);
        var brandText = config?.Brand ?? config?.Title ?? ThemeProvider.GetBrandText(_theme);
        var socialLinksHtml = await SocialLinksHtmlRenderer.BuildSocialLinksHtmlAsync(config?.SocialLinks, _iconsDir, _fallbackIconsDir);

        var seg = view.CanonicalPath.Trim('/');
        var siteNavHtml = SiteNavRenderer.Build(config, basePath, seg);
        var footerHtml = FooterRenderer.Build(config, basePath, brandText, _markdown);
        var pageSegment = seg.Length == 0 ? string.Empty : $"{seg}/";
        var rawPath = $"{basePath}/{pageSegment}".TrimStart('/');
        var canonicalUrl = $"{pageOrigin}/{rawPath}";

        var metaDescription = string.IsNullOrEmpty(view.Description) ? config?.Description : view.Description;
        var socialImageUrl = ResolveSocialImage(view.Image ?? config?.Image ?? config?.BrandImage, pageOrigin, basePath);
        var siteName = config?.Brand ?? config?.Title;
        var locale = Config.ResolveLocale(config)?.Code ?? "en";
        var modified = view.IsArticle ? view.Modified : null;

        var socialMetaHtml = SocialMetaRenderer.BuildSocialMeta(
            canonicalUrl, view.Title, metaDescription, !view.IsArticle, socialImageUrl, siteName, locale, modified);
        var structuredDataHtml = StructuredDataRenderer.BuildJsonLd(
            canonicalUrl, view.Title, metaDescription, !view.IsArticle, socialImageUrl, siteName, modified, nonce);

        var fullHtml = LayoutProvider.GetLayout(
            title: PageTitleRenderer.ComputeTitle(view.Title, config),
            content: view.ContentHtml,
            themeCss: themeCss,
            brandText: brandText,
            brandImage: config?.BrandImage,
            themeMode: themeSelection.Mode,
            enableLiveReload: _docsOptions.EnableHotReload,
            buildVersion: _content.BuildVersion,
            favicon: config?.Favicon,
            description: string.IsNullOrEmpty(view.Description) ? config?.Description : view.Description,
            noIndex: view.NoIndex,
            isHomePage: false,
            showScrollIndicator: config?.ScrollIndicator ?? ThemeProvider.ShowScrollIndicator(_theme),
            basePath: basePath,
            lang: Config.ResolveLocale(config)?.Code ?? "en",
            headTagsHtml: HeadTagHtmlRenderer.BuildHeadTagsHtml(config?.Head),
            canonicalUrl: canonicalUrl,
            nonce: nonce,
            hasMath: view.ContentHtml.Contains("class=\"katex\"", StringComparison.Ordinal),
            hasMermaid: hasMermaid,
            hasMap: view.ContentHtml.Contains("class=\"warden-map\"", StringComparison.Ordinal),
            isArticle: view.IsArticle,
            proseStyle: view.Prose,
            siteNavHtml: siteNavHtml,
            footerHtml: footerHtml,
            socialLinksHtml: socialLinksHtml,
            pageId: seg.Length == 0 ? "home" : seg.Replace('/', '-'),
            // User theme assets load last so custom.css overrides engine styles at equal specificity.
            customAssetsHtml: customCssLink + customJsScript,
            socialMetaHtml: socialMetaHtml,
            structuredDataHtml: structuredDataHtml,
            theme: activeTheme,
            structure: activeStructure);

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(fullHtml);
    }

    public Task Write404Async(HttpContext context)
    {
        var config = _content.SiteConfig;
        context.Response.StatusCode = 404;
        context.Response.ContentType = "text/html; charset=utf-8";
        return context.Response.WriteAsync(
            LayoutProvider.Get404Layout(
                LayoutProvider.HtmlEncode,
                _settings.BasePath,
                Config.ResolveLocale(config)?.Code ?? "en",
                ThemeSelection.Resolve(_theme, _settings.CliTheme, config?.Theme).Theme));
    }
}
