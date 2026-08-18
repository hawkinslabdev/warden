namespace Warden.Services.Theming;

/// <summary>Styling only: CSS custom properties plus an optional component CSS fragment. Never content, routing or front matter.</summary>
public interface IWardenTheme
{
    /// <summary>Kebab-case id used in <c>config.json</c> and <c>--theme</c>. Matched case-insensitively.</summary>
    string Name { get; }

    string Label { get; }

    /// <summary>Light-mode custom properties, e.g. <c>["--bg-color"] = "#ffffff"</c>.</summary>
    IReadOnlyDictionary<string, string> LightTokens { get; }

    /// <summary>Dark counterparts; a colour missing here bleeds through from light mode.</summary>
    IReadOnlyDictionary<string, string> DarkTokens { get; }

    /// <summary>Rules appended after the base stylesheet, inside the same nonce'd style element.</summary>
    string ComponentCss { get; }

    /// <summary>Colours every theme must define in both modes; the parity test enforces it.</summary>
    static IReadOnlyList<string> RequiredPaletteKeys { get; } =
    [
        "--bg-color",
        "--sidebar-bg",
        "--text-color",
        "--text-muted",
        "--accent",
        "--accent-light",
        "--border",
        "--code-bg"
    ];
}
