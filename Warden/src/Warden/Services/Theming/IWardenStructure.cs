namespace Warden.Services.Theming;

/// <summary>
/// Page shape only: layout, spacing, corner radius, never color. Orthogonal to <see cref="IWardenTheme"/>,
/// so any palette can pair with any structure (e.g. "ocean" colors with the "editorial" page shape).
/// </summary>
public interface IWardenStructure
{
    /// <summary>Kebab-case id used in <c>config.json</c> and <c>--structure</c>. Matched case-insensitively.</summary>
    string Name { get; }

    string Label { get; }

    /// <summary>Rules appended after the theme's own component CSS, inside the same nonce'd style element.</summary>
    string ComponentCss { get; }
}
