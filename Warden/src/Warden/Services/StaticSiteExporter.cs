using System.Net;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Warden.Configuration;

namespace Warden.Services;

public static class StaticSiteExporter
{
    public static async Task RunAsync(WebApplication app, string outputDir, string? baseUrl, CancellationToken cancellationToken)
    {
        app.Urls.Clear();
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync(cancellationToken);

        var address = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();

        using var client = new HttpClient { BaseAddress = new Uri(address) };

        var docs = app.Services.GetRequiredService<ContentService>();
        await docs.ForceRebuildAsync(cancellationToken);

        var pages = await docs.GetAllPagesAsync(cancellationToken);

        Directory.CreateDirectory(outputDir);

        var originPrefix = address.TrimEnd('/');
        var publicPrefix = string.IsNullOrEmpty(baseUrl) ? null : baseUrl.TrimEnd('/');
        var basePath = app.Services.GetRequiredService<PageRequestSettings>().BasePath;

        // (requestPath, output-relative directory). Empty dir => outputDir/index.html; "/" is the status page itself
        var routes = new List<(string Request, string Dir)> { ("/", "") };

        foreach (var page in pages.Where(p => p.Path.StartsWith("pages/", StringComparison.Ordinal)))
        {
            var slug = page.Path["pages/".Length..];
            if (slug.Length > 0) routes.Add(($"/{slug}", slug));
        }

        foreach (var (request, dir) in routes)
        {
            using var response = await client.GetAsync(request, cancellationToken);
            var html = WithCspMeta(
                await response.Content.ReadAsStringAsync(cancellationToken), response);
            if (publicPrefix is not null)
                html = html.Replace(originPrefix, publicPrefix);
            // rewrite warden.css and warden.js to page depth for file:// previews, while other assets stay absolute
            var depth = dir.Length == 0 ? 0 : dir.Split('/').Length;
            var relativePrefix = string.Concat(Enumerable.Repeat("../", depth));
            html = html.Replace($"{basePath}/warden.css", $"{relativePrefix}warden.css")
                       .Replace($"{basePath}/warden.js", $"{relativePrefix}warden.js");
            var targetFile = dir.Length == 0
                ? Path.Combine(outputDir, "index.html")
                : Path.Combine(outputDir, Path.Combine(dir.Split('/')), "index.html");
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            await File.WriteAllTextAsync(targetFile, html, cancellationToken);
        }

        foreach (var extra in new[] { "robots.txt", "sitemap.xml" })
        {
            var content = await client.GetStringAsync($"/{extra}", cancellationToken);
            if (publicPrefix is not null)
                content = content.Replace(originPrefix, publicPrefix);
            await File.WriteAllTextAsync(Path.Combine(outputDir, extra), content, cancellationToken);
        }

        // written once at the output root so local disk previews find it at their relative depth.
        foreach (var asset in new[] { "warden.css", "warden.js" })
        {
            using var assetResponse = await client.GetAsync($"/{asset}", cancellationToken);
            var bytes = await assetResponse.Content.ReadAsByteArrayAsync(cancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(outputDir, asset), bytes, cancellationToken);
        }

        using var notFoundResponse = await client.GetAsync("/__warden_export_404__", cancellationToken);
        var notFoundHtml = WithCspMeta(
            await notFoundResponse.Content.ReadAsStringAsync(cancellationToken), notFoundResponse);
        await File.WriteAllTextAsync(Path.Combine(outputDir, "404.html"), notFoundHtml, cancellationToken);

        CopyStaticAssets(app.Environment.WebRootPath, outputDir);

        var options = app.Services.GetRequiredService<Configuration.DocsOptions>();
        var assetsSrc = Path.Combine(Path.GetFullPath(options.RootPath), "assets");
        if (Directory.Exists(assetsSrc))
            CopyStaticAssets(assetsSrc, Path.Combine(outputDir, "assets"), AssetContentTypes.IsAllowed);

        await app.StopAsync(cancellationToken);
    }

    // csp was response-header only so published exports lacked it, and frame-ancestors is dropped since meta policies ignore it.
    private static string WithCspMeta(string html, HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Content-Security-Policy", out var values))
            return html;

        var policy = string.Join(";", values.First().Split(';')
            .Where(d => !d.TrimStart().StartsWith("frame-ancestors", StringComparison.OrdinalIgnoreCase)));

        var head = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
        if (head < 0) return html;

        var meta = $"\n    <meta http-equiv=\"Content-Security-Policy\" content=\"{WebUtility.HtmlEncode(policy)}\">";
        return html.Insert(head + "<head>".Length, meta);
    }

    // A static host serves what it is given, so the export applies the same media allowlist the runtime does.
    private static void CopyStaticAssets(string sourceRoot, string outputDir, Func<string, bool>? allow = null)
    {
        if (!Directory.Exists(sourceRoot)) return;

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            if (allow is not null && !allow(file)) continue;
            var relative = Path.GetRelativePath(sourceRoot, file);
            var dest = Path.Combine(outputDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }
}
