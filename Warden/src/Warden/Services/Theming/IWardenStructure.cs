namespace Warden.Services.Theming;

/// <summary>
/// Page shape only: layout, spacing, corner radius, never color. Orthogonal to <see cref="IWardenTheme"/>,
/// so any palette can pair with any structure.
/// </summary>
public interface IWardenStructure
{
    /// <summary>Kebab-case id used in <c>config.json</c> and <c>--structure</c>. Matched case-insensitively.</summary>
    string Name { get; }

    string Label { get; }

    /// <summary>Rules appended after the theme's own component CSS, inside the same nonce'd style element.</summary>
    string ComponentCss { get; }

    /// <summary>
    /// True renders the status page as monitors grouped by type into a card grid with response-time
    /// charts and a pinned ongoing-incidents panel; false (the default for every other structure) renders
    /// the plain flat monitor list. The one deliberate exception to "page shape only, never markup" above:
    /// a card grid is different DOM, not a CSS reskin of the list, so it can't be expressed as ComponentCss alone.
    /// </summary>
    bool UseGroupedStatusLayout => false;

    /// <summary>True renders the overall-uptime line and the pinned ongoing-incidents panel above the monitor list/grid. Defaults to following <see cref="UseGroupedStatusLayout"/>, so dashboard gets it for free; "default" overrides this to true while staying with the flat list.</summary>
    bool ShowStatusHeader => UseGroupedStatusLayout;
}
