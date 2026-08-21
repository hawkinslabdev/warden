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

    // CSP only ever existed as a response header, so a published export had no policy at all.
    // frame-ancestors is ignored in a meta policy and only logs a console warning, so it is dropped.
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
