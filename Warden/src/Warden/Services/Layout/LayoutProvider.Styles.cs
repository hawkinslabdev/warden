namespace Warden.Services.Layout;

public static partial class LayoutProvider
{
    private static GeneratedAsset? _cssAsset;

    private static readonly Lazy<string> BaseCss = new(ReadEmbeddedCss);

    private static string GetStylesLink(string themeTokenCss, string themeComponentCss, string basePath)
    {
        var asset = GetStylesAsset(themeTokenCss, themeComponentCss, basePath);
        return $"<link rel=\"stylesheet\" href=\"{basePath}/warden.css?v={asset.Version}\">";
    }

    internal static GeneratedAsset GetStylesAsset(string themeTokenCss, string themeComponentCss, string basePath) =>
        GetOrBuildAsset(ref _cssAsset, themeTokenCss + " " + themeComponentCss + " " + basePath,
            () => MinifyCss(themeTokenCss + "\n" + BaseCss.Value.Replace("__BASE_PATH__", basePath, StringComparison.Ordinal)
                + "\n" + themeComponentCss));

    private static string ReadEmbeddedCss()
    {
        using var stream = typeof(LayoutProvider).Assembly.GetManifestResourceStream("Warden.Resources.warden.css")
            ?? throw new InvalidOperationException("Embedded resource warden.css not found");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // CSS has no line comments or ASI to break, so collapsing all whitespace is safe here unlike in MinifyJs
    private static string MinifyCss(string css) =>
        System.Text.RegularExpressions.Regex.Replace(
            System.Text.RegularExpressions.Regex.Replace(css, @"/\*[\s\S]*?\*/", ""),
            @"[ \t]*[\r\n]+[ \t]*|[ \t]{2,}", " ").Trim();
}
