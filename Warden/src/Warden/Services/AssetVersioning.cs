using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Warden.Services;

public sealed class AssetVersioning
{
    public static AssetVersioning Current { get; set; } = new(null);

    private readonly string? _assetsDir;
    private readonly ConcurrentDictionary<string, (long Ticks, long Length, string Version)> _cache = new();

    public AssetVersioning(string? assetsDir) =>
        _assetsDir = assetsDir is null ? null : Path.GetFullPath(assetsDir);

    public string Apply(string url)
    {
        const string marker = "/assets/";
        var idx = url.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0 || url.Contains('?'))
            return url;

        var rel = Uri.UnescapeDataString(url[(idx + marker.Length)..]);
        var version = Version(rel);
        return version is null ? url : $"{url}?v={version}";
    }

    private string? Version(string rel)
    {
        if (_assetsDir is null || rel.Length == 0)
            return null;

        var full = Path.GetFullPath(Path.Combine(_assetsDir, rel));
        if (!full.StartsWith(_assetsDir + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(full))
            return null;

        var info = new FileInfo(full);
        if (_cache.TryGetValue(rel, out var hit) && hit.Ticks == info.LastWriteTimeUtc.Ticks && hit.Length == info.Length)
            return hit.Version;

        var version = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{info.LastWriteTimeUtc.Ticks}-{info.Length}")))[..8];
        _cache[rel] = (info.LastWriteTimeUtc.Ticks, info.Length, version);
        return version;
    }
}