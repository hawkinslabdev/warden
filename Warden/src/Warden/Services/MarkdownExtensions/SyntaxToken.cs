namespace Warden.Services.MarkdownExtensions;

/// <summary>LightColor/DarkColor null when unhighlighted.</summary>
public readonly record struct SyntaxToken(string Text, string? LightColor, string? DarkColor);
