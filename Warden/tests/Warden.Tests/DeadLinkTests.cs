using System.Text.RegularExpressions;
using Warden.Configuration;
using Warden.Models;
using Warden.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Warden.Tests;

public sealed partial class DeadLinkTests : IDisposable
{
    private readonly string _tempDir;
    private ContentService? _service;
    private readonly MarkdownService _markdown;

    public DeadLinkTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "warden-deadlink-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _markdown = new MarkdownService();
    }

    public void Dispose()
    {
        _service?.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private ContentService CreateService(string? configJson = null)
    {
        if (configJson != null)
            File.WriteAllText(Path.Combine(_tempDir, "config.json"), configJson);

        var options = new DocsOptions
        {
            RootPath = _tempDir,
            DefaultPage = "index",
            EnableHotReload = false
        };
        return new ContentService(options, _markdown, NullLogger<ContentService>.Instance);
    }

    [Fact]
    public async Task AllInternalLinks_ResolveToExistingPages()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "index.md"),
            "---\ntitle: Home\n---\n\n[Installation](/getting-started/installation/)\n[Configuration](/getting-started/configuration/)\n");
        Directory.CreateDirectory(Path.Combine(_tempDir, "getting-started"));
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "getting-started", "installation.md"),
            "---\ntitle: Installation\n---\n\nBack to [Home](/)\nSee [Configuration](../configuration)\n");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "getting-started", "configuration.md"),
            "---\ntitle: Configuration\n---\n\nBack to [Home](/)\n");

        _service = CreateService();
        await _service.StartAsync(CancellationToken.None);

        var pages = await _service.GetAllPagesAsync();
        var deadLinks = await FindDeadLinks(pages);

        Assert.Empty(deadLinks);
    }

    [Fact]
    public async Task DeadLink_Detected_ForMissingPage()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "index.md"),
            "---\ntitle: Home\n---\n\n[Go nowhere](/i-dont-exist/)\n[Also missing](../missing-page)\n");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "real-page.md"),
            "---\ntitle: Real\n---\n\n# Real\n");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "config.json"),
            """{"nav":[{"section":"Guide","items":[{"title":"Real","path":"real-page"}]}]}""");

        _service = CreateService();
        await _service.StartAsync(CancellationToken.None);

        var pages = await _service.GetAllPagesAsync();
        var deadLinks = await FindDeadLinks(pages);

        Assert.Contains(deadLinks, d => d.Link == "/i-dont-exist/");
        Assert.Contains(deadLinks, d => d.Link == "../missing-page");
        Assert.DoesNotContain(deadLinks, d => d.PagePath == "real-page");
    }

    [Fact]
    public async Task SkipsExternalLinksAndAnchors()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "index.md"),
            """
            # Home
            [External](https://example.com)
            [Anchor only](#some-heading)
            [Mail](mailto:test@example.com)
            [Protocol relative](//cdn.example.com/lib.js)
            """);

        _service = CreateService();
        await _service.StartAsync(CancellationToken.None);

        var pages = await _service.GetAllPagesAsync();
        var deadLinks = await FindDeadLinks(pages);

        Assert.Empty(deadLinks);
    }

    [Fact]
    public async Task RunOnRealDocs_NoDeadInternalLinks()
    {
        var docsDir = Path.Combine(AppContext.BaseDirectory, "docs");
        if (!Directory.Exists(docsDir))
            return;

        var options = new DocsOptions
        {
            RootPath = docsDir,
            DefaultPage = "index",
            EnableHotReload = false
        };
        _service = new ContentService(options, _markdown, NullLogger<ContentService>.Instance);
        await _service.StartAsync(CancellationToken.None);

        var pages = await _service.GetAllPagesAsync();
        var deadLinks = await FindDeadLinks(pages);

        Assert.Empty(deadLinks);
    }

    private async Task<List<(string PagePath, string Link)>> FindDeadLinks(IReadOnlyList<DocumentationPage> pages)
    {
        var deadLinks = new List<(string PagePath, string Link)>();
        foreach (var page in pages)
        {
            foreach (Match match in HrefRegex().Matches(page.HtmlContent))
            {
                var href = match.Groups[1].Value;
                if (ShouldSkip(href))
                    continue;

                var resolved = ResolvePath(page.Path, href);
                if (resolved.Length == 0)
                    continue;

                var target = await _service!.GetPageAsync(resolved);
                if (target == null)
                    deadLinks.Add((page.Path, href));
            }
        }
        return deadLinks;
    }

    private static string ResolvePath(string pagePath, string href)
    {
        // Strip fragment (e.g. "../deploy#section" -> "../deploy")
        var fragIdx = href.IndexOf('#');
        var pathOnly = fragIdx >= 0 ? href[..fragIdx] : href;

        if (pathOnly.StartsWith('/'))
            return pathOnly.Trim('/').ToLowerInvariant();

        // Relative links resolve against the page path as a directory, and index is served at / not /index/.
        var basePath = pagePath == "index" ? "" : pagePath;
        var combined = $"{basePath}/{pathOnly}";
        var segments = new List<string>();
        foreach (var seg in combined.Split('/'))
        {
            if (seg == "..")
            {
                if (segments.Count > 0)
                    segments.RemoveAt(segments.Count - 1);
            }
            else if (seg != "." && seg != "")
                segments.Add(seg);
        }
        return string.Join("/", segments).ToLowerInvariant();
    }

    private static bool ShouldSkip(string href)
    {
        return href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("//")
            || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("#")
            || href == "/";
    }

    [GeneratedRegex(@"<a\s[^>]*href=""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HrefRegex();
}
