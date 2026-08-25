namespace Warden.Models;

using System.Text.Json.Serialization;

public class Config
{
    public string? Title { get; set; }
    public string? TitleTemplate { get; set; }
    public string? Description { get; set; }
    public string? Lang { get; set; }
    public List<HeadTag>? Head { get; set; }

    public static LocaleOptions? ResolveLocale(Config? config)
    {
        if (config is null || (config.Locale is null && config.Culture is null && config.Lang is null))
            return null;

        return new LocaleOptions
        {
            Culture = config.Locale?.Culture ?? config.Culture,
            Code = config.Locale?.Code ?? config.Locale?.Lang ?? config.Lang
        };
    }

    /// <summary>Theme name, e.g. <c>"ocean"</c>. Unknown names fall back to the default theme.</summary>
    public string? Theme { get; set; }

    /// <summary>Page structure name. <c>"clean"</c> is the only one Warden ships and is also the default, so leaving this unset or setting it to <c>"clean"</c> are the same thing. Orthogonal to <see cref="Theme"/>; any other name logs a warning and falls back to <c>"clean"</c>.</summary>
    public string? Structure { get; set; }

    public string? Brand { get; set; }
    public string? BrandImage { get; set; }
    public string? Image { get; set; }
    public string? Footer { get; set; }
    public string? Favicon { get; set; }

    /// <summary>Site owner name, e.g. for the <c>{author}</c> token in <c>footer</c>. "author" is a Teatime holdover; Organization/Organisation/Owner are aliases, checked in that order, for whichever term fits a company, team, or homelab operator.</summary>
    public string? Author { get; set; }

    /// <summary>Alias for <see cref="Author"/>.</summary>
    public string? Organization { get; set; }

    /// <summary>Alias for <see cref="Author"/> - British spelling of <see cref="Organization"/>.</summary>
    public string? Organisation { get; set; }

    /// <summary>Alias for <see cref="Author"/> - fits a homelab run by one person who isn't writing content, just running the server.</summary>
    public string? Owner { get; set; }

    /// <summary>Header nav items.</summary>
    public List<MenuLink>? Menu { get; set; }

    /// <summary>Footer links.</summary>
    public List<MenuLink>? FooterMenu { get; set; }

    /// <summary>Top reading-progress bar. Defaults to on; set false to hide it.</summary>
    public bool? ScrollIndicator { get; set; }

    /// <summary>The status page's overall-uptime line. Defaults to on; set false to hide it.</summary>
    public bool? ShowOverallUptime { get; set; }

    /// <summary>Noindex whole surfaces site-wide, without touching front matter per page.
    /// A page's own <c>noindex</c> front matter still applies when its surface here is off.</summary>
    public NoIndexOptions? NoIndex { get; set; }

    public List<SocialLink>? SocialLinks { get; set; }

    /// <summary>Root-level date culture (e.g. "en-GB"), merged with <c>locale.culture</c>.</summary>
    public string? Culture { get; set; }

    /// <summary>Locale settings: date culture and the locale table. Accepts the object form
    /// <c>{ "culture": "en-GB", "code": "en" }</c> or a bare code string <c>"en"</c>.</summary>
    [JsonConverter(typeof(LocaleOptionsConverter))]
    public LocaleOptions? Locale { get; set; }

    /// <summary>Hosts a front matter <c>redirect:</c> may send readers to off-site. Same-host redirects always work.</summary>
    public List<string>? RedirectHosts { get; set; }

    // what warden checks itself, and how often
    public MonitoringConfig? Monitoring { get; set; }
}

/// <summary>Per-surface noindex switches for <see cref="Config.NoIndex"/>.</summary>
public sealed class NoIndexOptions
{
    /// <summary>Noindex every standalone page under <c>content/pages/</c> (and drop them from <c>sitemap.xml</c>).</summary>
    public bool? Pages { get; set; }

    /// <summary>Noindex the status page itself ("/"), same opt-out shape as <see cref="Pages"/>: unset/<c>false</c> keeps
    /// it indexed, <c>true</c> keeps search engines and AI crawlers off it.</summary>
    public bool? Status { get; set; }
}
