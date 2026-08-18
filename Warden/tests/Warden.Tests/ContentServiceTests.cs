using System.Reflection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Warden.Configuration;
using Warden.Models;
using Warden.Services;

namespace Warden.Tests;

public sealed class ContentServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ContentService _service;
    private readonly DocsOptions _options;
    private readonly MarkdownService _markdown;

    public ContentServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "warden-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "getting-started"));

        _options = new DocsOptions
        {
            RootPath = _tempDir,
            DefaultPage = "index",
            EnableHotReload = false
        };
        _markdown = new MarkdownService();
        _service = new ContentService(_options, _markdown, NullLogger<ContentService>.Instance);
    }

    public void Dispose()
    {
        _service.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private async Task CreateTestFiles()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "index.md"),
            "---\ntitle: Home\ndescription: Welcome page\n---\n\n# homepage\n\nWelcome to the docs.\n");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "getting-started", "installation.md"),
            "---\ntitle: Installation\n---\n\n# Installation Guide\n\nHow to install.\n");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "getting-started", "configuration.md"),
            "---\ntitle: Configuration\n---\n\n# Configuration Guide\n\nHow to configure.\n");
    }

    [Fact]
    public async Task StartAsync_BuildsPageCache()
    {
        await CreateTestFiles();
        await _service.StartAsync(CancellationToken.None);

        var home = await _service.GetPageAsync("index");
        Assert.NotNull(home);
        Assert.Equal("Home", home!.Title);

        var install = await _service.GetPageAsync("getting-started/installation");
        Assert.NotNull(install);
        Assert.Equal("Installation", install!.Title);
    }

    [Fact]
    public async Task GetPageAsync_ReturnsNull_ForMissingPage()
    {
        await _service.StartAsync(CancellationToken.None);
        var page = await _service.GetPageAsync("nonexistent");
        Assert.Null(page);
    }

    [Fact]
    public async Task GetPageAsync_NormalizesPath()
    {
        await CreateTestFiles();
        await _service.StartAsync(CancellationToken.None);

        var page = await _service.GetPageAsync("/Getting-Started/Installation/");
        Assert.NotNull(page);
        Assert.Equal("Installation", page!.Title);
    }

    [Fact]
    public async Task GetAllPagesAsync_ReturnsAllPages()
    {
        await CreateTestFiles();
        await _service.StartAsync(CancellationToken.None);

        var pages = await _service.GetAllPagesAsync();
        Assert.Equal(3, pages.Count);
    }

    [Fact]
    public async Task StartAsync_PageWithoutFrontMatterTitle_FallsBackToConfigNavTitle()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "index.md"), "# Home\n");
        Directory.CreateDirectory(Path.Combine(_tempDir, "getting-started"));
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "getting-started", "routing.md"),
            "# Some heading unrelated to the nav title\n");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "config.json"), """
            {
              "nav": [
                {
                  "section": "Guide",
                  "items": [
                    { "title": "routing", "path": "getting-started/routing" }
                  ]
                }
              ]
            }
            """);

        await _service.StartAsync(CancellationToken.None);

        var page = await _service.GetPageAsync("getting-started/routing");
        Assert.NotNull(page);
        Assert.Equal("routing", page!.Title);
    }

    [Fact]
    public async Task StartAsync_FrontMatterTitle_TakesPrecedenceOverConfigNavTitle()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "index.md"), "# Home\n");
        Directory.CreateDirectory(Path.Combine(_tempDir, "getting-started"));
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "getting-started", "routing.md"),
            "---\ntitle: Custom Routing Title\n---\n\n# Routing\n");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "config.json"), """
            {
              "nav": [
                {
                  "section": "Guide",
                  "items": [
                    { "title": "routing", "path": "getting-started/routing" }
                  ]
                }
              ]
            }
            """);

        await _service.StartAsync(CancellationToken.None);

        var page = await _service.GetPageAsync("getting-started/routing");
        Assert.NotNull(page);
        Assert.Equal("Custom Routing Title", page!.Title);
    }

    [Fact]
    public async Task StartAsync_ConfigDev_OverridesConfig()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "index.md"), "# Home\n");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "config.json"), """{ "title": "base" }""");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "config.json.dev"), """{ "title": "dev" }""");

        await _service.StartAsync(CancellationToken.None);

        Assert.Equal("dev", _service.SiteConfig?.Title);
    }

    [Fact]
    public async Task StartAsync_BrokenConfigDev_FallsBackToConfig()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "index.md"), "# Home\n");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "config.json"), """{ "title": "base" }""");
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "config.json.dev"), "{ not json ");

        await _service.StartAsync(CancellationToken.None);

        Assert.Equal("base", _service.SiteConfig?.Title);
    }

    [Fact]
    public async Task StartAsync_PageWithPaginationFalse_ShowPaginationFalse()
    {
        await CreateTestFiles();
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "getting-started", "installation.md"),
            "---\ntitle: Installation\npagination: false\n---\n\n# Installation Guide\n\nHow to install.\n");
        await _service.StartAsync(CancellationToken.None);

        var page = await _service.GetPageAsync("getting-started/installation");
        Assert.NotNull(page);
        Assert.False(page!.ShowPagination);
    }

    [Fact]
    public async Task StartAsync_PageWithoutPagination_ShowPaginationTrue()
    {
        await CreateTestFiles();
        await _service.StartAsync(CancellationToken.None);

        var page = await _service.GetPageAsync("getting-started/installation");
        Assert.NotNull(page);
        Assert.True(page!.ShowPagination);
    }

    [Fact]
    public async Task StartAsync_PageWithRedirect_ReturnsRedirect()
    {
        await CreateTestFiles();
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "getting-started", "installation.md"),
            "---\nredirect: /guide/setup\n---\n");
        await _service.StartAsync(CancellationToken.None);

        var page = await _service.GetPageAsync("getting-started/installation");
        Assert.NotNull(page);
        Assert.Equal("/guide/setup", page!.Redirect);
    }

    [Fact]
    public async Task StartAsync_PageWithFrontmatterDate_UsesItForLastModified()
    {
        await CreateTestFiles();
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "getting-started", "installation.md"),
            "---\ntitle: Installation\ndate: 2025-03-01\n---\n\n# Installation Guide\n");
        await _service.StartAsync(CancellationToken.None);

        var page = await _service.GetPageAsync("getting-started/installation");
        Assert.NotNull(page);
        Assert.Equal(new DateTime(2025, 3, 1), page!.LastModified);
    }

    [Fact]
    public async Task StartAsync_PageWithFrontmatterUpdated_OverridesFileMtime()
    {
        await CreateTestFiles();
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "getting-started", "installation.md"),
            "---\ntitle: Installation\ndate: 2025-01-01\nupdated: 2025-06-28\n---\n\n# Installation Guide\n");
        await _service.StartAsync(CancellationToken.None);

        var page = await _service.GetPageAsync("getting-started/installation");
        Assert.NotNull(page);
        Assert.Equal(new DateTime(2025, 6, 28), page!.LastModified);
    }

}
