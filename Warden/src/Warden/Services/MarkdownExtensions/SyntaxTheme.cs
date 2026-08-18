namespace Warden.Services.MarkdownExtensions;

/// <summary>Light/dark theme colors for syntax-highlighted code blocks.</summary>
public readonly record struct SyntaxTheme(
    string LightName,
    string DarkName,
    string LightForeground,
    string DarkForeground);
