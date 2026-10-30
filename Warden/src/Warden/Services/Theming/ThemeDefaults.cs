namespace Warden.Services.Theming;

/// <summary>Non-palette properties shared by every theme; a theme merges its palette over these.</summary>
public static class ThemeDefaults
{
    /// <summary>Emitted in <c>:root</c> before the active theme's light tokens.</summary>
    public static IReadOnlyDictionary<string, string> Light { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--alert-note"] = "#0969da",
        ["--alert-tip"] = "#1a7f37",
        ["--alert-important"] = "#8250df",
        ["--alert-warning"] = "#9a6700",
        ["--alert-caution"] = "#cf222e",
        // fixed across themes
        ["--status-ok"] = "#276C40",
        ["--status-warn"] = "#8A5C00",
        ["--status-down"] = "#B42318",
        ["--status-maint"] = "#3B5BA5",
        ["--font-sans"] = "\"Inter\", system-ui, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, sans-serif",
        ["--font-display"] = "\"Inter Display\", \"Inter\", system-ui, -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, sans-serif",
        ["--font-mono"] = "ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace",
        ["--measure"] = "680px",
        ["--measure-wide"] = "760px",
        ["--topbar-height"] = "57px",
        ["--nav-hover-bg"] = "var(--code-bg)",
        ["--nav-active-bg"] = "var(--accent-light)",
        ["--overlay-bg"] = "rgba(0, 0, 0, 0.5)",
        ["--code-button-bg"] = "var(--bg-color)",
        ["--code-button-border"] = "var(--border)",
        ["--code-button-hover"] = "var(--accent)",
        ["--shadow-md"] = "0 8px 24px rgba(0, 0, 0, 0.12)",
        ["--shadow-lg"] = "0 24px 64px rgba(0, 0, 0, 0.3)",
    };

    /// <summary>Dark deltas only. Absent keys inherit the light block, which is right for alias vars and wrong for literals.</summary>
    public static IReadOnlyDictionary<string, string> Dark { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--alert-note"] = "#2f81f7",
        ["--alert-tip"] = "#3fb950",
        ["--alert-important"] = "#a371f7",
        ["--alert-warning"] = "#d4a72c",
        ["--alert-caution"] = "#f85149",
        ["--status-ok"] = "#5FB37A",
        ["--status-warn"] = "#D4A72C",
        ["--status-down"] = "#F0705F",
        ["--status-maint"] = "#8EA8E8",
        ["--shadow-md"] = "0 8px 24px rgba(0, 0, 0, 0.45)",
        ["--shadow-lg"] = "0 24px 64px rgba(0, 0, 0, 0.55)",
    };
}
