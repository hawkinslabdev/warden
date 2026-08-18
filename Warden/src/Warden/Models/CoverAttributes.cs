using System.Text.RegularExpressions;

namespace Warden.Models;

/// <summary>Parses a cover value into an image URL and a space-joined list of CSS classes.</summary>
public static partial class CoverAttributes
{
    private static readonly HashSet<string> Allowed =
        new(StringComparer.OrdinalIgnoreCase) { "natural", "plain", "wide", "full", "short", "tall" };

    public static (string Url, string? Classes) Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (raw?.Trim() ?? string.Empty, null);

        var m = AttrRegex().Match(raw);
        if (!m.Success) return (raw.Trim(), null);

        var classes = m.Groups[2].Value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.TrimStart('.').ToLowerInvariant())
            .Where(Allowed.Contains)
            .ToArray();

        return (m.Groups[1].Value.Trim(), classes.Length > 0 ? string.Join(' ', classes) : null);
    }

    [GeneratedRegex(@"^(.*?)\s*\{([^}]*)\}\s*$")]
    private static partial Regex AttrRegex();
}
