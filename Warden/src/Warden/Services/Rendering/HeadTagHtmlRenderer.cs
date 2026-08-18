using System.Text;
using System.Text.Encodings.Web;
using Warden.Models;

namespace Warden.Services.Rendering;

public static class HeadTagHtmlRenderer
{
    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "meta", "link", "base"
    };

    public static string BuildHeadTagsHtml(List<HeadTag>? tags)
    {
        if (tags is null or { Count: 0 }) return string.Empty;

        // Lets pre-allocate capacity to avoid array resizing
        var sb = new StringBuilder(512);
        
        using var writer = new StringWriter(sb);

        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag.Tag)) continue;

            sb.Append('<').Append(tag.Tag);

            if (tag.Attrs is { Count: > 0 } attrs)
            {
                foreach (var (key, value) in attrs)
                {
                    sb.Append(' ').Append(key).Append("=\"");

                    HtmlEncoder.Default.Encode(writer, value);
                    
                    sb.Append('"');
                }
            }

            if (VoidElements.Contains(tag.Tag))
            {
                sb.AppendLine(">");
            }
            else
            {
                sb.Append('>');
                if (tag.Content is not null)
                {
                    if (IsRawElement(tag.Tag))
                    {
                        sb.Append(tag.Content);
                    }
                    else
                    {
                        HtmlEncoder.Default.Encode(writer, tag.Content);
                    }
                }
                sb.Append("</").Append(tag.Tag).AppendLine(">");
            }
        }

        return sb.ToString();
    }

    private static bool IsRawElement(string tag)
    {
        return tag.Equals("script", StringComparison.OrdinalIgnoreCase) || 
               tag.Equals("style", StringComparison.OrdinalIgnoreCase);
    }
}