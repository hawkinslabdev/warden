using Warden.Services.Layout;

namespace Warden.Services.Rendering;

public static class UrlPaths
{
    public static string Href(string basePath, string path)
    {
        var trimmed = path.Trim('/');
        return trimmed.Length == 0
            ? (basePath.Length == 0 ? "/" : $"{basePath}/")
            : $"{basePath}/{LayoutProvider.HtmlEncode(trimmed)}/";
    }
}
