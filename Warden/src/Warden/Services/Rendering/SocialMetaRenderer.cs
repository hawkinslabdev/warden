using System.Text;
using Warden.Services.Layout;

namespace Warden.Services.Rendering;

public static class SocialMetaRenderer
{
    public static string BuildSocialMeta(
        string? canonicalUrl,
        string title,
        string? description,
        bool isHomePage,
        string? imageUrl = null,
        string? siteName = null,
        string? locale = null,
        DateTime? modified = null)
    {
        if (string.IsNullOrEmpty(canonicalUrl))
            return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"    <meta property=\"og:type\" content=\"{(isHomePage ? "website" : "article")}\">");
        sb.AppendLine($"    <meta property=\"og:title\" content=\"{LayoutProvider.HtmlEncode(title)}\">");
        sb.AppendLine($"    <meta property=\"og:url\" content=\"{LayoutProvider.HtmlEncode(canonicalUrl)}\">");

        if (!string.IsNullOrEmpty(siteName))
            sb.AppendLine($"    <meta property=\"og:site_name\" content=\"{LayoutProvider.HtmlEncode(siteName)}\">");

        if (!string.IsNullOrEmpty(locale))
            sb.AppendLine($"    <meta property=\"og:locale\" content=\"{LayoutProvider.HtmlEncode(locale)}\">");

        if (!string.IsNullOrEmpty(description))
        {
            sb.AppendLine($"    <meta property=\"og:description\" content=\"{LayoutProvider.HtmlEncode(description)}\">");
            sb.AppendLine($"    <meta name=\"twitter:description\" content=\"{LayoutProvider.HtmlEncode(description)}\">");
        }

        if (!isHomePage && modified is { } m)
            sb.AppendLine($"    <meta property=\"article:modified_time\" content=\"{m.ToUniversalTime():yyyy-MM-ddTHH:mm:ssZ}\">");

        if (!string.IsNullOrEmpty(imageUrl))
        {
            sb.AppendLine($"    <meta property=\"og:image\" content=\"{LayoutProvider.HtmlEncode(imageUrl)}\">");
            sb.AppendLine($"    <meta name=\"twitter:image\" content=\"{LayoutProvider.HtmlEncode(imageUrl)}\">");
            sb.AppendLine("    <meta name=\"twitter:card\" content=\"summary_large_image\">");
        }
        else
        {
            sb.AppendLine("    <meta name=\"twitter:card\" content=\"summary\">");
        }

        sb.AppendLine($"    <meta name=\"twitter:title\" content=\"{LayoutProvider.HtmlEncode(title)}\">");
        return sb.ToString();
    }
}
