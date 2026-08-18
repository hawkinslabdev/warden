using Warden.Models;
using Warden.Services.Layout;

namespace Warden.Services.Rendering;

/// <summary>Cover image and prev/next nav chrome shared by every content page.</summary>
public static class PageChromeRenderer
{
    public static string BuildCover(string? rawCover, string basePath)
    {
        if (string.IsNullOrWhiteSpace(rawCover)) return string.Empty;
        var (url, classes) = CoverAttributes.Parse(rawCover);
        if (url.Length == 0) return string.Empty;
        var css = classes is { Length: > 0 } ? $"post-cover {classes}" : "post-cover";
        return RenderCover(url, css, basePath);
    }

    private static string RenderCover(string cover, string css, string basePath) =>
        CoverColor.TryParse(cover, out var hex)
            ? $"<div class=\"{css}\" style=\"background:{hex}\" aria-hidden=\"true\"></div>"
            : $"<img class=\"{css}\" src=\"{LayoutProvider.HtmlEncode(Asset(basePath, cover))}\" alt=\"\" loading=\"eager\" fetchpriority=\"high\" decoding=\"async\">";

    private static string Asset(string basePath, string url)
    {
        var resolved = url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal)
            ? $"{basePath}{url}"
            : url;
        return AssetVersioning.Current.Apply(resolved);
    }

    public static string BuildAdjacentNav(string? prevHref, string? prevTitle, string? nextHref, string? nextTitle, string ariaLabel)
    {
        if (prevHref is null && nextHref is null) return string.Empty;

        var l = Localization.Current;
        var olderHtml = prevHref is not null
            ? $"<a class=\"post-nav-link post-nav-older\" rel=\"prev\" href=\"{prevHref}\"><span class=\"post-nav-label\">← {LayoutProvider.HtmlEncode(l.PostNavPrevious)}</span><span class=\"post-nav-title\">{LayoutProvider.HtmlEncode(prevTitle)}</span></a>"
            : "<span></span>";
        var newerHtml = nextHref is not null
            ? $"<a class=\"post-nav-link post-nav-newer\" rel=\"next\" href=\"{nextHref}\"><span class=\"post-nav-label\">{LayoutProvider.HtmlEncode(l.PostNavNext)} →</span><span class=\"post-nav-title\">{LayoutProvider.HtmlEncode(nextTitle)}</span></a>"
            : "<span></span>";

        return $"<nav class=\"post-nav\" aria-label=\"{LayoutProvider.HtmlEncode(ariaLabel)}\">{olderHtml}{newerHtml}</nav>";
    }
}
