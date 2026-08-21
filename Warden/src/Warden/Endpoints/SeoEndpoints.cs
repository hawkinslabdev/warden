using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Warden.Configuration;
using Warden.Models;
using Warden.Services;
using Warden.Services.Rendering;

namespace Warden.Endpoints;

internal static class SeoEndpoints
{
    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapMethods("/robots.txt", HttpVerbs.GetAndHead, GetRobots);
        app.MapMethods("/sitemap.xml", HttpVerbs.GetAndHead, GetSitemap);
        return app;
    }

    internal static ContentHttpResult GetRobots(HttpContext context, PageRequestSettings settings)
    {
        var baseUrl = settings.Origin(context);
        var body = $"User-agent: *\nAllow: /\nSitemap: {baseUrl}{settings.BasePath}/sitemap.xml\n";
        return TypedResults.Text(body, "text/plain", Encoding.UTF8);
    }

    internal static async Task<ContentHttpResult> GetSitemap(ContentService content, PageRequestSettings settings, HttpContext context)
    {
        var basePath = settings.BasePath;
        var config = content.SiteConfig;
        var pages = await content.GetAllPagesAsync(context.RequestAborted);

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        var noIndex = config?.NoIndex;
        if (!(noIndex?.Status ?? false))
            sb.AppendLine($"  <url><loc>{UrlPaths.Href(basePath, "")}</loc><priority>1.0</priority></url>");

        foreach (var page in pages.Where(p => p.InSitemap && !p.NoIndex && !(noIndex?.Pages ?? false) && p.Path.StartsWith("pages/", StringComparison.Ordinal)))
        {
            var slug = page.Path["pages/".Length..];
            if (slug.Length == 0) continue;
            var lastMod = (page.LastModified ?? DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            sb.AppendLine($"  <url><loc>{UrlPaths.Href(basePath, slug)}</loc><lastmod>{lastMod}</lastmod><priority>0.5</priority></url>");
        }

        sb.AppendLine("</urlset>");
        return TypedResults.Text(sb.ToString(), "application/xml", Encoding.UTF8);
    }
}
