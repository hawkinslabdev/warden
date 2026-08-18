using System.Linq;

namespace Warden.Services.MarkdownExtensions;

/// <summary>Token color pair plus highlighted-word-match flag.</summary>
internal readonly record struct RenderedSpan(string Text, string? LightColor, string? DarkColor, bool IsHighlightedWord);

/// <summary>Splits tokens at word-match boundaries so matched slices can be wrapped in a `highlighted-word` span.</summary>
internal static class CodeWordHighlighter
{
    public static List<RenderedSpan> Apply(IReadOnlyList<SyntaxToken> lineTokens, IReadOnlySet<string> words)
    {
        var spans = new List<RenderedSpan>(lineTokens.Count);
        foreach (var token in lineTokens)
            spans.Add(new RenderedSpan(token.Text, token.LightColor, token.DarkColor, false));

        if (words.Count == 0)
            return spans;

        var lineText = string.Concat(spans.Select(s => s.Text));

        foreach (var word in words)
        {
            if (string.IsNullOrEmpty(word))
                continue;

            var index = lineText.IndexOf(word, StringComparison.Ordinal);
            while (index >= 0)
            {
                HighlightRange(spans, index, word.Length);
                index = lineText.IndexOf(word, index + word.Length, StringComparison.Ordinal);
            }
        }

        return spans;
    }

    private static void HighlightRange(List<RenderedSpan> spans, int start, int length)
    {
        var end = start + length;
        var current = 0;

        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            var spanStart = current;
            var spanEnd = current + span.Text.Length;
            current = spanEnd;

            if (spanEnd <= start || spanStart >= end)
                continue;

            var overlapStart = Math.Max(0, start - spanStart);
            var overlapEnd = Math.Min(span.Text.Length, end - spanStart);
            if (overlapEnd <= overlapStart)
                continue;

            var pieces = new List<RenderedSpan>(3);
            if (overlapStart > 0)
                pieces.Add(span with { Text = span.Text[..overlapStart] });
            pieces.Add(span with { Text = span.Text[overlapStart..overlapEnd], IsHighlightedWord = true });
            if (overlapEnd < span.Text.Length)
                pieces.Add(span with { Text = span.Text[overlapEnd..] });

            spans.RemoveAt(i);
            spans.InsertRange(i, pieces);
            i += pieces.Count - 1;
        }
    }
}
