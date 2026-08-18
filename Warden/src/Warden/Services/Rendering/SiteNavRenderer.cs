using System.Text;
using Warden.Models;
using Warden.Services.Layout;

namespace Warden.Services.Rendering;

public static class SiteNavRenderer
{
    private const string Chevron =
        "<svg class=\"top-nav-chevron\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" aria-hidden=\"true\"><path d=\"M6 9l6 6 6-6\" stroke-linecap=\"round\" stroke-linejoin=\"round\"></path></svg>";

    private static readonly (string Text, string Path)[] Default =
    [
        ("Home", ""),
    ];

    public static string Build(Config? config, string basePath, string currentPath)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"site-nav-wrap\"><nav class=\"site-nav\" aria-label=\"Primary\">");

        // Undeclared (null) falls back to the default menu; declared-but-empty means no menu at all.
        if (config?.Menu is { } menu)
        {
            foreach (var item in menu)
                AppendEntry(sb, item, basePath, currentPath);
        }
        else
        {
            foreach (var (text, path) in Default)
                AppendLink(sb, text, path, basePath, currentPath);
        }

        sb.Append("</nav></div>");
        return sb.ToString();
    }

    private static void AppendEntry(StringBuilder sb, MenuLink item, string basePath, string currentPath)
    {
        if (string.IsNullOrWhiteSpace(item.Title)) return;

        if (item.Items is { Count: > 0 } children)
        {
            AppendDropdown(sb, item.Title!, children, basePath, currentPath);
            return;
        }

        if (string.IsNullOrWhiteSpace(item.Path)) return;
        AppendLink(sb, item.Title!, item.Path!, basePath, currentPath, item.External);
    }

    private static void AppendDropdown(StringBuilder sb, string text, List<MenuLink> children, string basePath, string currentPath)
    {
        // Only mark the exact page as "here," not parent sections that happen to match part of the name
        var activeSeg = children
            .Where(c => !string.IsNullOrWhiteSpace(c.Path) && !IsExternal(c.Path!))
            .Select(c => c.Path!.Trim('/').ToLowerInvariant())
            .Where(seg => IsActive(seg, currentPath))
            .OrderByDescending(seg => seg.Length)
            .FirstOrDefault();

        sb.Append("<div class=\"top-nav-item has-dropdown\">");
        sb.Append("<button type=\"button\" class=\"top-nav-link").Append(activeSeg is not null ? " active" : "")
          .Append("\" aria-expanded=\"false\" aria-haspopup=\"true\">")
          .Append(LayoutProvider.HtmlEncode(text)).Append(' ').Append(Chevron).Append("</button>");
        sb.Append("<div class=\"top-nav-dropdown-menu\">");
        foreach (var child in children)
        {
            if (string.IsNullOrWhiteSpace(child.Title) || string.IsNullOrWhiteSpace(child.Path)) continue;
            AppendDropdownLink(sb, child.Title!, child.Path!, basePath, currentPath, activeSeg, child.External);
        }
        sb.Append("</div></div>");
    }

    private static void AppendLink(StringBuilder sb, string text, string link, string basePath, string currentPath, bool external = false)
    {
        if (IsExternal(link))
        {
            sb.Append("<a href=\"").Append(LayoutProvider.HtmlEncode(link))
              .Append("\" target=\"_blank\" rel=\"noopener noreferrer\">").Append(LayoutProvider.HtmlEncode(text)).Append("</a>");
            return;
        }

        var seg = link.Trim('/').ToLowerInvariant();
        var href = UrlPaths.Href(basePath, seg);
        sb.Append("<a href=\"").Append(href).Append('"');
        if (external) sb.Append(" target=\"_blank\" rel=\"noopener noreferrer\"");
        else if (IsActive(seg, currentPath)) sb.Append(" class=\"here\" aria-current=\"page\"");
        sb.Append('>').Append(LayoutProvider.HtmlEncode(text)).Append("</a>");
    }

    private static void AppendDropdownLink(StringBuilder sb, string text, string link, string basePath, string currentPath, string? activeSeg, bool external = false)
    {
        if (IsExternal(link))
        {
            sb.Append("<a class=\"top-nav-dropdown-link\" href=\"").Append(LayoutProvider.HtmlEncode(link))
              .Append("\" target=\"_blank\" rel=\"noopener noreferrer\">").Append(LayoutProvider.HtmlEncode(text)).Append("</a>");
            return;
        }

        var seg = link.Trim('/').ToLowerInvariant();
        var href = UrlPaths.Href(basePath, seg);
        sb.Append("<a class=\"top-nav-dropdown-link").Append(seg == activeSeg ? " here" : "")
          .Append("\" href=\"").Append(href).Append('"');
        if (external) sb.Append(" target=\"_blank\" rel=\"noopener noreferrer\"");
        sb.Append('>').Append(LayoutProvider.HtmlEncode(text)).Append("</a>");
    }

    private static bool IsActive(string itemSeg, string currentPath)
    {
        if (itemSeg.Length == 0)
            return currentPath.Length == 0;

        return currentPath == itemSeg
            || currentPath.StartsWith($"{itemSeg}/", StringComparison.Ordinal);
    }

    private static bool IsExternal(string link) =>
        link.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || link.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        || link.StartsWith("//", StringComparison.Ordinal)
        || link.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);
}
