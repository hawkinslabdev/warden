using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

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
            var html = await client.GetStringAsync(request, cancellationToken);
            if (publicPrefix is not null)
                html = html.Replace(originPrefix, publicPrefix);
            var targetFile = dir.Length == 0
                ? Path.Combine(outputDir, "index.html")
                : Path.Combine(outputDir, Path.Combine(dir.Split('/')), "index.html");
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            await File.WriteAllTextAsync(targetFile, html, cancellationToken);
        }

        foreach (var extra in new[] { "robots.txt", "llms.txt", "sitemap.xml" })
        {
            var content = await client.GetStringAsync($"/{extra}", cancellationToken);
            if (publicPrefix is not null)
                content = content.Replace(originPrefix, publicPrefix);
            await File.WriteAllTextAsync(Path.Combine(outputDir, extra), content, cancellationToken);
        }

        var notFoundResponse = await client.GetAsync("/__warden_export_404__", cancellationToken);
        var notFoundHtml = await notFoundResponse.Content.ReadAsStringAsync(cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputDir, "404.html"), notFoundHtml, cancellationToken);

        CopyStaticAssets(app.Environment.WebRootPath, outputDir);

        var options = app.Services.GetRequiredService<Configuration.DocsOptions>();
        var assetsSrc = Path.Combine(Path.GetFullPath(options.RootPath), "assets");
        if (Directory.Exists(assetsSrc))
            CopyStaticAssets(assetsSrc, Path.Combine(outputDir, "assets"));

        await app.StopAsync(cancellationToken);
    }

    private static void CopyStaticAssets(string sourceRoot, string outputDir)
    {
        if (!Directory.Exists(sourceRoot)) return;

        foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, file);
            var dest = Path.Combine(outputDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }
}
