using System.Text.Json;
using System.Text.Json.Serialization;

namespace Warden.Services.Rendering;

public static class StructuredDataRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string BuildJsonLd(
        string? canonicalUrl,
        string title,
        string? description,
        bool isHomePage,
        string? imageUrl = null,
        string? siteName = null,
        DateTime? modified = null,
        string? nonce = null)
    {
        if (string.IsNullOrEmpty(canonicalUrl))
            return string.Empty;

        object node;
        if (isHomePage)
        {
            node = new Dictionary<string, object?>
            {
                ["@type"] = "WebSite",
                ["name"] = siteName ?? title,
                ["url"] = canonicalUrl,
                ["description"] = description
            };
        }
        else
        {
            var iso = modified?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            node = new Dictionary<string, object?>
            {
                ["@type"] = "Article",
                ["headline"] = title,
                ["description"] = description,
                ["mainEntityOfPage"] = canonicalUrl,
                ["image"] = imageUrl,
                ["datePublished"] = iso,
                ["dateModified"] = iso,
                ["publisher"] = string.IsNullOrEmpty(siteName)
                    ? null
                    : new Dictionary<string, object?> { ["@type"] = "Organization", ["name"] = siteName }
            };
        }

        var doc = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org"
        };
        foreach (var (key, value) in (Dictionary<string, object?>)node)
            doc[key] = value;

        var json = JsonSerializer.Serialize(doc, JsonOptions);
        var nonceAttr = nonce is { Length: > 0 } ? $" nonce=\"{nonce}\"" : string.Empty;
        return $"    <script type=\"application/ld+json\"{nonceAttr}>{json}</script>\n";
    }
}
