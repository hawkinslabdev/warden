using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Warden.Configuration;
using Warden.Models;
using Warden.Services.Rendering;

namespace Warden.Services;

public sealed partial class ContentService : IHostedService, IDisposable
{
    private readonly DocsOptions _options;
    private readonly MarkdownService _markdown;
    private readonly ILogger<ContentService> _logger;
    private readonly string _basePathSegment;
    private FileSystemWatcher? _watcher;
    private FileSystemWatcher? _configWatcher;
    private FileSystemWatcher? _assetsWatcher;
    private FileSystemWatcher? _localeWatcher;
    private readonly CancellationTokenSource _shutdownCts = new();
    private Task _consumerTask = Task.CompletedTask;

    // All read state lives in one immutable snapshot swapped atomically after a full build; readers never see half-built state
    private sealed record ContentSnapshot(
        IReadOnlyDictionary<string, DocumentationPage> Pages,
        Config? Config);

    private static readonly ContentSnapshot EmptySnapshot = new(
        ImmutableDictionary<string, DocumentationPage>.Empty,
        null);

    private volatile ContentSnapshot _snapshot = EmptySnapshot;
    private string? _lastContentHash;
    private readonly SemaphoreSlim _buildLock = new(1, 1);
    private readonly Channel<FileSystemEventArgs> _fileChannel =
        Channel.CreateBounded<FileSystemEventArgs>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    private bool _disposed;

    public ContentService(
        DocsOptions options,
        MarkdownService markdown,
        ILogger<ContentService> logger)
    {
        _options = options;
        _markdown = markdown;
        _logger = logger;
        _basePathSegment = _options.BasePath?.Trim('/').ToLowerInvariant() ?? "";
    }

    public Task ForceRebuildAsync(CancellationToken cancellationToken) => RebuildAsync(cancellationToken);

    public Config? SiteConfig => _snapshot.Config;

    public long BuildVersion { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await RebuildAsync(cancellationToken);

        if (_options.EnableHotReload)
        {
            var docsPath = Path.GetFullPath(_options.RootPath);
            if (!Directory.Exists(docsPath))
                Directory.CreateDirectory(docsPath);

            _watcher = new FileSystemWatcher(docsPath)
            {
                IncludeSubdirectories = true,
                Filter = "*.md",
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileChanged;
            _watcher.Renamed += OnFileRenamed;

            // Root-level JSON only: config.json, extensions.json and their .dev overrides. Subdirectories stay out of scope.
            _configWatcher = new FileSystemWatcher(docsPath)
            {
                Filter = "*.json*",
                EnableRaisingEvents = true
            };
            _configWatcher.Changed += OnConfigFileChanged;
            _configWatcher.Created += OnConfigFileChanged;
            _configWatcher.Deleted += OnConfigFileChanged;
            _configWatcher.Renamed += OnConfigFileRenamed;

            var assetsPath = Path.Combine(docsPath, "assets");
            if (Directory.Exists(assetsPath))
            {
                _assetsWatcher = new FileSystemWatcher(assetsPath)
                {
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };
                _assetsWatcher.Changed += OnFileChanged;
                _assetsWatcher.Created += OnFileChanged;
                _assetsWatcher.Deleted += OnFileChanged;
                _assetsWatcher.Renamed += OnFileRenamed;
            }

            // Main watcher filters *.md; locale JSON needs its own.
            var localePath = Path.Combine(docsPath, "locale");
            if (Directory.Exists(localePath))
            {
                _localeWatcher = new FileSystemWatcher(localePath)
                {
                    Filter = "*.json",
                    EnableRaisingEvents = true
                };
                _localeWatcher.Changed += OnFileChanged;
                _localeWatcher.Created += OnFileChanged;
                _localeWatcher.Deleted += OnFileChanged;
                _localeWatcher.Renamed += OnFileRenamed;
            }

            _consumerTask = FileWatcherConsumerAsync(_shutdownCts.Token);

            _logger.LogInformation("Hot reload enabled, watching {DocsPath}", docsPath);
        }
    }

    // both StopAsync and Dispose can be called (host teardown, test fixture teardown), from either order or thread;
    // this runs the actual shutdown exactly once, so cancel/dispose of _shutdownCts never races itself
    private int _shutdownState;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _shutdownState, 1, 0) != 0) return;
        _shutdownCts.Cancel();
        await _consumerTask;
        _watcher?.Dispose();
        _configWatcher?.Dispose();
        _assetsWatcher?.Dispose();
        _localeWatcher?.Dispose();
        _shutdownCts.Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (Interlocked.CompareExchange(ref _shutdownState, 1, 0) == 0)
        {
            _shutdownCts.Cancel();
            _consumerTask.GetAwaiter().GetResult();
            _watcher?.Dispose();
            _configWatcher?.Dispose();
            _assetsWatcher?.Dispose();
            _localeWatcher?.Dispose();
            _shutdownCts.Dispose();
        }
        _buildLock.Dispose();
    }

    // Root JSON files allowed to affect a build (config.json, extensions.json, and .dev overrides).
    [GeneratedRegex(@"^(config|extensions)\.json(\.dev)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WatchedJsonFileRegex();

    // Content, assets, and locale watchers use scoped filters, so all events are relevant.
    private void OnFileChanged(object sender, FileSystemEventArgs e) =>
        _fileChannel.Writer.TryWrite(e);

    private void OnFileRenamed(object sender, RenamedEventArgs e) =>
        _fileChannel.Writer.TryWrite(e);

    // Restricts the broad root JSON watcher to only config.json, extensions.json, and their .dev overrides.
    private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
    {
        if (IsWatchedJson(e.Name))
            _fileChannel.Writer.TryWrite(e);
    }

    private void OnConfigFileRenamed(object sender, RenamedEventArgs e)
    {
        if (IsWatchedJson(e.Name) || IsWatchedJson(e.OldName))
            _fileChannel.Writer.TryWrite(e);
    }

    private static bool IsWatchedJson(string? fileName) =>
        !string.IsNullOrEmpty(fileName) && WatchedJsonFileRegex().IsMatch(fileName);

    private async Task FileWatcherConsumerAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var __ in _fileChannel.Reader.ReadAllAsync(ct))
            {
                await Task.Delay(300, ct);

                while (_fileChannel.Reader.TryRead(out _)) { }

                try
                {
                    await RebuildAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to rebuild documentation");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File watcher consumer failed");
        }
    }

    private async Task RebuildAsync(CancellationToken cancellationToken)
    {
        await _buildLock.WaitAsync(cancellationToken);
        try
        {
            await BuildAsync(cancellationToken);
        }
        finally
        {
            _buildLock.Release();
        }
    }

    // Caller must hold _buildLock; builds a complete snapshot off to the side, then swaps it in
    private async Task BuildAsync(CancellationToken cancellationToken)
    {
        IconProvider.ClearCache();
        var docsPath = Path.GetFullPath(_options.RootPath);
        if (!Directory.Exists(docsPath))
        {
            _logger.LogWarning("Content directory does not exist: {Path}", docsPath);
            return;
        }

        var config = LoadConfig(docsPath);

        DateFormatter.Current = DateFormatter.From(Config.ResolveLocale(config));
        Localization.Current = Localization.From(docsPath, config, _logger);

        // Sorted for deterministic hashing, regardless of FS enumeration order.
        var allFiles = Directory.GetFiles(docsPath, "*.md", SearchOption.AllDirectories).Order().ToArray();
        var pages = new List<DocumentationPage>();
        var pageMap = new Dictionary<string, DocumentationPage>();
        var hashInput = new StringBuilder();

        foreach (var file in allFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = Path.GetRelativePath(docsPath, file);
            var pagePath = PagePath.FromFile(relativePath);

            var content = await File.ReadAllTextAsync(file, cancellationToken);
            hashInput.Append(relativePath).Append('\0').Append(content).Append('\0');

            var defaultTitle = Path.GetFileNameWithoutExtension(relativePath);
            if (defaultTitle.Equals("index", StringComparison.OrdinalIgnoreCase))
            {
                var dir = Path.GetDirectoryName(relativePath);
                if (!string.IsNullOrEmpty(dir))
                {
                    var dirName = Path.GetFileName(dir)!;
                    var spaced = dirName.Replace('-', ' ').Replace('_', ' ');
                    defaultTitle = spaced.Length > 0 ? char.ToUpperInvariant(spaced[0]) + spaced[1..] : dirName;
                }
                else
                {
                    defaultTitle = "Home";
                }
            }

            var normalizedRelativePath = relativePath.Replace('\\', '/');
            var parsed = _markdown.Parse(content, defaultTitle, filePath: normalizedRelativePath);

            var html = WrapTables(parsed.Html);
            html = VersionAssets(html);
            var lastModified = parsed.FrontmatterDate ?? File.GetLastWriteTimeUtc(file);

            var page = new DocumentationPage(
                Path: pagePath,
                Title: parsed.Title ?? defaultTitle,
                HtmlContent: html,
                Description: parsed.Description,
                LastModified: lastModified,
                Headings: parsed.Headings,
                Layout: parsed.Layout,
                ShowLastUpdated: parsed.ShowLastUpdated,
                OriginalRelativePath: normalizedRelativePath,
                Keywords: parsed.Keywords,
                ShowPagination: parsed.ShowPagination,
                Redirect: parsed.Redirect,
                Date: parsed.PublishDate,
                Cover: parsed.Cover,
                PageNext: parsed.PageNext,
                PagePrev: parsed.PagePrev,
                Updated: parsed.FrontmatterDate,
                InSitemap: parsed.InSitemap,
                NoIndex: parsed.NoIndex,
                Maintenance: parsed.Maintenance,
                End: parsed.End,
                Monitors: parsed.Monitors
            );

            pageMap[pagePath] = page;
            pages.Add(page);
        }

        var configPath = ResolveJsonFile(docsPath, "config.json");
        if (File.Exists(configPath))
            hashInput.Append(await File.ReadAllTextAsync(configPath, cancellationToken));

        // Locale JSON drives the locale table only, never enumerated as pages; hash it so an edit bumps BuildVersion.
        var localeDir = Path.Combine(docsPath, "locale");
        if (Directory.Exists(localeDir))
            foreach (var localeFile in Directory.GetFiles(localeDir, "*.json").Order())
                hashInput.Append(Path.GetFileName(localeFile)).Append('\0')
                         .Append(await File.ReadAllTextAsync(localeFile, cancellationToken)).Append('\0');

        // Fold asset size+timestamp into the hash so a media change bumps BuildVersion (live-reload trigger).
        var assetsDir = Path.Combine(docsPath, "assets");
        if (Directory.Exists(assetsDir))
            foreach (var asset in Directory.GetFiles(assetsDir, "*", SearchOption.AllDirectories).Order())
            {
                var info = new FileInfo(asset);
                hashInput.Append(Path.GetRelativePath(assetsDir, asset)).Append('\0')
                         .Append(info.LastWriteTimeUtc.Ticks).Append('\0')
                         .Append(info.Length).Append('\0');
            }

        var contentHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput.ToString())));

        var snapshot = new ContentSnapshot(
            pageMap,
            config);

        _snapshot = snapshot;

        // Prevent unnecessary client reloads from spurious file events by verifying content changes!
        if (contentHash == _lastContentHash)
        {
            _logger.LogDebug("Rebuilt documentation but content is unchanged, skipping version bump");
            return;
        }

        _lastContentHash = contentHash;
        BuildVersion++;
        _logger.LogInformation("Built documentation with {PageCount} pages", pages.Count);

        LogDeadLinks(pages, pageMap);
    }

    // Routes the app serves that no content file backs; dead-link checking treats these as live.
    private static readonly HashSet<string> KnownRoutes = new(StringComparer.Ordinal)
    {
        "", "sitemap.xml", "robots.txt", "llms.txt",
    };

    private void LogDeadLinks(List<DocumentationPage> pages, Dictionary<string, DocumentationPage> pageMap)
    {
        var deadSources = new HashSet<string>();
        foreach (var page in pages)
        {
            foreach (Match match in HrefRegex().Matches(page.HtmlContent))
            {
                var href = match.Groups[1].Value;
                if (ShouldSkipHref(href))
                    continue;

                var resolved = ResolveHref(page.Path, href, _basePathSegment);
                if (resolved.Length == 0
                    || pageMap.ContainsKey(resolved)
                    || pageMap.ContainsKey($"pages/{resolved}")
                    || KnownRoutes.Contains(resolved))
                    continue;

                deadSources.Add(page.Path);
            }
        }

        if (deadSources.Count > 0)
        {
            var list = string.Join(", ", deadSources.Order());
            _logger.LogWarning("Dead internal links found in: {Sources}", list);
        }
    }

    private static string ResolveHref(string pagePath, string href, string basePathSegment)
    {
        var fragIdx = href.IndexOf('#');
        var pathOnly = fragIdx >= 0 ? href[..fragIdx] : href;

        if (pathOnly.StartsWith('/'))
        {
            var abs = pathOnly.Trim('/').ToLowerInvariant();
            if (basePathSegment.Length > 0)
            {
                if (abs == basePathSegment) return "";
                if (abs.StartsWith($"{basePathSegment}/", StringComparison.Ordinal))
                    abs = abs[(basePathSegment.Length + 1)..];
            }
            return abs;
        }

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

    private static bool ShouldSkipHref(string href)
    {
        return href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("//")
            || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("#")
            || href.Contains("/assets/", StringComparison.OrdinalIgnoreCase)
            || href == "/";
    }

    [GeneratedRegex(@"<a\s[^>]*href=""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HrefRegex();

    public ValueTask<DocumentationPage?> GetPageAsync(string path, CancellationToken cancellationToken = default)
    {
        path = path.Trim('/').ToLowerInvariant();
        if (string.IsNullOrEmpty(path))
            path = _options.DefaultPage ?? "index";

        _snapshot.Pages.TryGetValue(path, out var page);
        return ValueTask.FromResult(page);
    }

    public Task<IReadOnlyList<DocumentationPage>> GetAllPagesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DocumentationPage> pages = _snapshot.Pages.Values.ToImmutableList();
        return Task.FromResult(pages);
    }

    [GeneratedRegex(@"<table[^>]*>[\s\S]*?</table>", RegexOptions.IgnoreCase)]
    private static partial Regex TableRegex();

    private static string WrapTables(string html) =>
        TableRegex().Replace(html, m => $"<div class=\"table-wrapper\">{m.Value}</div>");

    private static string VersionAssets(string html) =>
        AssetSrcHrefRegex().Replace(html, m =>
        {
            var url = m.Groups[2].Value;
            var versioned = AssetVersioning.Current.Apply(url);
            return versioned == url ? m.Value : $"{m.Groups[1].Value}=\"{versioned}\"";
        });

    [GeneratedRegex(@"(src|href)=""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AssetSrcHrefRegex();

    /// <summary>Prefers <c>NAME.json.dev</c> over <c>NAME.json</c> when the former exists and parses, for a local override that never ships.</summary>
    private static string ResolveJsonFile(string docsPath, string fileName)
    {
        var devPath = Path.Combine(docsPath, fileName + ".dev");
        if (File.Exists(devPath) && ParsesAsJson(devPath))
            return devPath;
        return Path.Combine(docsPath, fileName);
    }

    private static bool ParsesAsJson(string path)
    {
        try
        {
            using var _ = JsonDocument.Parse(File.ReadAllText(path));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Config? LoadConfig(string docsPath)
    {
        var configPath = ResolveJsonFile(docsPath, "config.json");
        if (!File.Exists(configPath))
            return null;

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }
}
