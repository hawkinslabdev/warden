using System.Linq;

namespace Warden.Services.MarkdownExtensions;

/// <summary>No-op <see cref="ISyntaxHighlighter"/>: each line comes back as one plain token. Also the fallback on failure.</summary>
public sealed class NullSyntaxHighlighter : ISyntaxHighlighter
{
    public static readonly NullSyntaxHighlighter Instance = new();

    public SyntaxTheme? Theme => null;

    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public IReadOnlyList<IReadOnlyList<SyntaxToken>> TokenizeLines(IReadOnlyList<string> lines, string lang) =>
        lines.Select(line => (IReadOnlyList<SyntaxToken>)[new SyntaxToken(line, null, null)]).ToList();
}
