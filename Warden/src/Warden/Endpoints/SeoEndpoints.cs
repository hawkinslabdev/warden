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
    // These bodies embed an absolute origin taken from the Host header, so a shared cache must not
    // serve one host's sitemap under another's key. PageResponder does the same for page responses.
    private static void VaryOnHost(HttpContext context, PageRequestSettings settings)
    {
        if (settings.PublicBaseUrl is null)
            context.Response.Headers.Vary = "Host";
    }

    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapMethods("/robots.txt", HttpVerbs.GetAndHead, GetRobots);
        app.MapMethods("/sitemap.xml", HttpVerbs.GetAndHead, GetSitemap);
        app.MapMethods("/incidents/feed.xml", HttpVerbs.GetAndHead, GetIncidentFeed);
        return app;
    }

    internal static ContentHttpResult GetRobots(HttpContext context, PageRequestSettings settings)
    {
        VaryOnHost(context, settings);
        var baseUrl = settings.Origin(context);
        var body = $"User-agent: *\nAllow: /\nSitemap: {baseUrl}{settings.BasePath}/sitemap.xml\n";
        return TypedResults.Text(body, "text/plain", Encoding.UTF8);
    }

    internal static async Task<ContentHttpResult> GetSitemap(ContentService content, PageRequestSettings settings, HttpContext context)
    {
        VaryOnHost(context, settings);
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

    private const int FeedMaxEntries = 50;

    // atom, newest change first; an incident re-enters the top when it is resolved
    internal static async Task<ContentHttpResult> GetIncidentFeed(ContentService content, PageRequestSettings settings, HttpContext context)
    {
        VaryOnHost(context, settings);
        var origin = settings.Origin(context);
        var basePath = settings.BasePath;
        var pages = await content.GetAllPagesAsync(context.RequestAborted);
        var title = content.SiteConfig?.Title ?? "Warden";

        var entries = pages
            .Where(p => p.OriginalRelativePath?.StartsWith("incidents/", StringComparison.Ordinal) == true)
            .Select(p => (Page: p, Updated: IncidentContent.EndOf(p) ?? IncidentContent.StartOf(p)))
            .OrderByDescending(e => e.Updated)
            .Take(FeedMaxEntries)
            .ToList();

        var settingsXml = new System.Xml.XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false), Async = true };
        using var buffer = new MemoryStream();
        await using (var xml = System.Xml.XmlWriter.Create(buffer, settingsXml))
        {
            await xml.WriteStartDocumentAsync();
            await xml.WriteStartElementAsync(null, "feed", "http://www.w3.org/2005/Atom");
            await xml.WriteElementStringAsync(null, "title", null, $"{title} incidents");
            await xml.WriteElementStringAsync(null, "id", null, $"{origin}{basePath}/incidents/feed.xml");
            await xml.WriteElementStringAsync(null, "updated", null, (entries.Count > 0 ? entries[0].Updated : DateTimeOffset.UtcNow).ToString("O"));
            await xml.WriteStartElementAsync(null, "link", null);
            await xml.WriteAttributeStringAsync(null, "rel", null, "self");
            await xml.WriteAttributeStringAsync(null, "href", null, $"{origin}{basePath}/incidents/feed.xml");
            await xml.WriteEndElementAsync();
            await xml.WriteStartElementAsync(null, "link", null);
            await xml.WriteAttributeStringAsync(null, "href", null, $"{origin}{UrlPaths.Href(basePath, "")}");
            await xml.WriteEndElementAsync();

            foreach (var (page, updated) in entries)
            {
                var url = $"{origin}{UrlPaths.Href(basePath, page.Path)}";
                var state = page.Maintenance ? IncidentContent.MaintenanceBadgeClass(page, DateTimeOffset.UtcNow) : IncidentContent.IncidentBadgeClass(page);
                await xml.WriteStartElementAsync(null, "entry", null);
                await xml.WriteElementStringAsync(null, "id", null, url);
                await xml.WriteElementStringAsync(null, "title", null, page.Title);
                await xml.WriteElementStringAsync(null, "updated", null, updated.ToString("O"));
                await xml.WriteElementStringAsync(null, "published", null, IncidentContent.StartOf(page).ToString("O"));
                await xml.WriteStartElementAsync(null, "link", null);
                await xml.WriteAttributeStringAsync(null, "href", null, url);
                await xml.WriteEndElementAsync();
                await xml.WriteStartElementAsync(null, "category", null);
                await xml.WriteAttributeStringAsync(null, "term", null, page.Maintenance ? $"maintenance/{state}" : $"incident/{state}");
                await xml.WriteEndElementAsync();
                if (!string.IsNullOrWhiteSpace(page.Description))
                    await xml.WriteElementStringAsync(null, "summary", null, page.Description);
                await xml.WriteEndElementAsync();
            }

            await xml.WriteEndElementAsync();
            await xml.WriteEndDocumentAsync();
        }

        return TypedResults.Text(Encoding.UTF8.GetString(buffer.ToArray()), "application/atom+xml", Encoding.UTF8);
    }
}
