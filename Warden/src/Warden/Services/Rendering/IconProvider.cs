using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Warden.Services.Rendering;

public static partial class IconProvider
{
    private const int MaxCacheEntries = 256;

    private static readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);

    private static void CacheIfRoom(string iconName, string? svg)
    {
        if (_cache.Count < MaxCacheEntries)
            _cache.TryAdd(iconName, svg);
    }

    public static async ValueTask<string> InlineSvgAsync(string iconName, string primaryDir, string? fallbackDir = null)
    {
        if (_cache.TryGetValue(iconName, out var cached))
            return cached ?? string.Empty;

        var slug = Slugify(iconName);
        var filePath = Path.Combine(primaryDir, $"{slug}.svg");

        if (!File.Exists(filePath) && fallbackDir != null)
            filePath = Path.Combine(fallbackDir, $"{slug}.svg");

        if (!File.Exists(filePath))
        {
            CacheIfRoom(iconName, null);
            return string.Empty;
        }

        var svg = await File.ReadAllTextAsync(filePath);
        svg = StripFillAttr().Replace(svg, "");
        svg = svg.Replace("<svg", "<svg fill=\"currentColor\" aria-hidden=\"true\"");
        CacheIfRoom(iconName, svg);
        return svg;
    }

    public static void ClearCache() => _cache.Clear();

    private static string Slugify(string name) =>
        SlugRegex().Replace(name.ToLowerInvariant(), "-").Trim('-');

    [GeneratedRegex(@"\s+fill=""[^""]*""")]
    private static partial Regex StripFillAttr();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugRegex();
}
