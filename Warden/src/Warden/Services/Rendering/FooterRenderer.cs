using Warden.Models;
using Warden.Services.Layout;

namespace Warden.Services.Rendering;

public static class FooterRenderer
{
    public static string Build(Config? config, string basePath, string brandText, MarkdownService markdown)
    {
        var hasCustomNote = !string.IsNullOrWhiteSpace(config?.Footer);
        var links = FooterMenuRenderer.Build(config?.FooterMenu, basePath);

        if (!hasCustomNote && links.Length == 0)
            return string.Empty;

        var note = config?.Footer?
            .Replace("{year}", DateTime.UtcNow.Year.ToString())
            .Replace("{author}", config.Organization ?? config.Organisation ?? config.Owner ?? config.Author ?? string.Empty)
            .Replace("{title}", config.Title ?? string.Empty);
        note = !string.IsNullOrEmpty(note)
            ? markdown.ToHtml(note).Replace("<p>", "").Replace("</p>", "").Trim()
            : $"© {DateTime.UtcNow.Year} {LayoutProvider.HtmlEncode(brandText)}";

        return $@"<footer class=""site-footer"">
        <span class=""site-footer-note"">{note}</span>
        {links}
    </footer>";
    }
}
