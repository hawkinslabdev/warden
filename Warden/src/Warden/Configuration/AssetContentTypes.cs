using Microsoft.AspNetCore.StaticFiles;

namespace Warden.Configuration;

// Media allowlist for content/assets/. No scripts, HTML or archives: assets are contributed alongside content.
// Shared with the static export, which used to copy the directory wholesale and publish what the runtime 404s.
public static class AssetContentTypes
{
    public static readonly IReadOnlyDictionary<string, string> Map =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".webp"] = "image/webp",
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".svg"] = "image/svg+xml",
            [".avif"] = "image/avif",
            [".ico"] = "image/x-icon",
            [".pdf"] = "application/pdf",
            [".txt"] = "text/plain",
            [".woff2"] = "font/woff2",
            [".woff"] = "font/woff",
            [".mp4"] = "video/mp4",
            [".webm"] = "video/webm",
            [".mp3"] = "audio/mpeg",
        };

    public static bool IsAllowed(string path) =>
        Map.ContainsKey(Path.GetExtension(path));

    public static FileExtensionContentTypeProvider Provider() =>
        new(new Dictionary<string, string>(Map, StringComparer.OrdinalIgnoreCase));
}
