using YamlDotNet.Serialization;

namespace Warden.Models;

public sealed record FrontMatter
{
    /// <summary>Optional "next" pagination target for a standalone page (path or URL).</summary>
    [YamlMember(Alias = "page-next", ApplyNamingConventions = false)]
    public string? PageNext { get; init; }

    [YamlMember(Alias = "page-prev", ApplyNamingConventions = false)]
    public string? PagePrev { get; init; }

    [YamlMember(Alias = "page-previous", ApplyNamingConventions = false)]
    public string? PagePrevious { get; init; }

    public string? Title { get; init; }
    public string? Description { get; init; }

    public string? Layout { get; init; }

    public List<string>? Keywords { get; init; }

    /// <summary>Per-page override for <c>Config.LastUpdated</c>. <c>false</c> hides the
    /// "Last updated" stamp on this page even when the site-wide setting is on.</summary>
    public bool? LastUpdated { get; init; }

    /// <summary>Set to <c>false</c> to hide prev/next pagination links on this page.</summary>
    public bool? Pagination { get; init; }


    /// <summary>When set, the page issues a 307 redirect to this URL instead of rendering.
    /// Root-relative paths (starting with <c>/</c>) are prefixed with the configured base path.
    /// Absolute URLs are used as-is.</summary>
    public string? Redirect { get; init; }

    /// <summary>Content creation date (ISO 8601). Used as the "Last updated" display value
    /// when <see cref="Updated"/> is absent. Overrides file system mtime.</summary>
    public DateTime? Date { get; init; }

    /// <summary>Last-modified date (ISO 8601). Takes priority over <see cref="Date"/> and
    /// file system mtime for the "Last updated" display.</summary>
    public DateTime? Updated { get; init; }

    /// <summary>Feature image URL for the page.</summary>
    public string? Cover { get; init; }

    /// <summary>Set <c>false</c> to drop this page from <c>sitemap.xml</c>. Page stays live and searchable.</summary>
    public bool? Sitemap { get; init; }

    /// <summary>Set <c>true</c> to keep this page live but tell search engines not to index it
    /// (<c>&lt;meta name="robots" content="noindex, follow"&gt;</c> + <c>X-Robots-Tag</c> header).
    /// Also drops it from <c>sitemap.xml</c>.</summary>
    [YamlMember(Alias = "noindex", ApplyNamingConventions = false)]
    public bool? NoIndex { get; init; }

    /// <summary>content/incidents/ only: true marks this file a maintenance window instead of an incident; Date is the window's start.</summary>
    public bool? Maintenance { get; init; }

    /// <summary>content/incidents/ only: for an incident, presence marks it resolved; a maintenance window needs both this and Date, and drops off once End passes.</summary>
    public DateTime? End { get; init; }

    /// <summary>Alias for Date: "start" reads more naturally than "date" on an incident/maintenance file.</summary>
    public DateTime? Start { get; init; }

    /// <summary>content/incidents/ incidents only: <c>degraded</c> marks the monitors in <see cref="Monitors"/>
    /// impaired rather than down while this incident is open. Anything else (including unset) means down.</summary>
    public string? Status { get; init; }

    /// <summary>content/incidents/ maintenance: true only: monitor ids this window covers; while active, those monitors show a Maintenance badge instead of Up/Down.</summary>
    public List<string>? Monitors { get; init; }
}
