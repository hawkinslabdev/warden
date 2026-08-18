namespace Warden.Models;

public sealed record ThemeOptions
{
    /// <summary>Deployment-level theme override. Outranks <c>config.json</c>'s <c>theme</c>, loses to <c>--theme</c>.</summary>
    public string? Name { get; init; }

    public string? PrimaryColor { get; init; }
    public string? BgColor { get; init; }
    public string? SidebarBg { get; init; }
    public string? TextColor { get; init; }
    public string? TextMuted { get; init; }
    public string? BorderColor { get; init; }
    public string? CodeBg { get; init; }
    public string? AccentLight { get; init; }
    public string? FontSans { get; init; }
    public string? FontMono { get; init; }
    public string? CustomCssUrl { get; init; }
    public string? CustomJsUrl { get; init; }
    public string? BrandText { get; init; }
    public bool DarkMode { get; init; } = true;

    public string? Mode { get; init; }

    /// <summary>Thin top-of-viewport progress bar that fills as the page scrolls. On by default.</summary>
    public bool ShowScrollIndicator { get; init; } = true;
}
