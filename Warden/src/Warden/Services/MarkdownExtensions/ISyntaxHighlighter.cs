namespace Warden.Services.MarkdownExtensions;

/// <summary>Tokenizes whole blocks; must never throw -- return one plain <see cref="SyntaxToken"/> per line instead.</summary>
public interface ISyntaxHighlighter
{
    /// <summary>One-time setup (e.g. loading grammars/themes), called once at startup.</summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Null when no real highlighter is active.</summary>
    SyntaxTheme? Theme { get; }

    /// <summary>Tokenizes <paramref name="lines"/> for <paramref name="lang"/>; must not throw.</summary>
    IReadOnlyList<IReadOnlyList<SyntaxToken>> TokenizeLines(IReadOnlyList<string> lines, string lang);
}
