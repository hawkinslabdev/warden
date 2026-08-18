using System.Text.RegularExpressions;

namespace Warden.Services.MarkdownExtensions;

/// <summary>Result of processing line-notation comments and meta highlight ranges.</summary>
public sealed record CodeNotationResult(
    IReadOnlyList<string> Lines,
    IReadOnlyList<IReadOnlySet<string>> LineClasses,
    IReadOnlyList<IReadOnlySet<string>> LineWordHighlights,
    IReadOnlySet<string> ContainerClasses);

/// <summary>Parses comment-notation markers from raw source text, before tokenization (marker stripping must happen first either way).</summary>
public static class CodeNotationProcessor
{
    private static readonly (string Name, string[] Classes, string? ContainerClass)[] Effects =
    [
        ("highlight", ["highlighted"], "has-highlighted"),
        ("hl", ["highlighted"], "has-highlighted"),
        ("++", ["diff", "add"], "has-diff"),
        ("--", ["diff", "remove"], "has-diff"),
        ("focus", ["has-focus"], "has-focused-lines"),
        ("error", ["highlighted", "error"], "has-highlighted"),
        ("warning", ["highlighted", "warning"], "has-highlighted"),
        ("info", ["highlighted", "info"], "has-highlighted"),
    ];

    private static readonly Regex MarkerRegex = BuildCommentMarkerRegex(
        names => $"(?<name>{names})(?::(?<count>\\d+))?",
        string.Join('|', Effects.Select(e => Regex.Escape(e.Name))));

    private static readonly Regex WordMarkerRegex = BuildCommentMarkerRegex(
        _ => @"word:(?<word>(?:\\.|[^:\]])+)(?::(?<count>\d+))?",
        string.Empty);

    private static readonly Regex WordUnescapeRegex = new(@"\\(.)", RegexOptions.Compiled);

    private static Regex BuildCommentMarkerRegex(Func<string, string> innerPattern, string namesArg)
    {
        var inner = innerPattern(namesArg);
        // Same named groups reused across alternatives -- ok since only one alternative can match.
        var pattern =
            $@"^(?<pre>.*?)(?:" +
            $@"<!--\s*\[!code\s+{inner}\]\s*-->|" +
            $@"/\*\s*\[!code\s+{inner}\]\s*\*/|" +
            $@"(?://|\#|;{{1,2}}|%{{1,2}}|--)\s*\[!code\s+{inner}\]" +
            $@")\s*$";
        return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public static CodeNotationResult Process(IReadOnlyList<string> rawLines, IReadOnlySet<int> metaHighlightedLines)
    {
        var outLines = new List<string>(rawLines.Count);
        var outClasses = new List<IReadOnlySet<string>>(rawLines.Count);
        var outWords = new List<IReadOnlySet<string>>(rawLines.Count);
        var containerClasses = new HashSet<string>();
        var pendingClasses = new List<(string[] Classes, int Remaining)>();
        var pendingWords = new List<(string Word, int Remaining)>();

        var originalLineNumber = 0;
        foreach (var raw in rawLines)
        {
            originalLineNumber++;

            var (content, isDropped) = ProcessEffectsOrWord(
                raw, pendingClasses, pendingWords, containerClasses,
                out var matchedHere, out var wordMatchedHere);

            if (isDropped)
                continue;

            var lineClasses = ApplyPendingClasses(pendingClasses, matchedHere);
            var lineWords = ApplyPendingWords(pendingWords, wordMatchedHere);

            if (metaHighlightedLines.Contains(originalLineNumber))
                lineClasses.Add("highlighted");

            outLines.Add(content);
            outClasses.Add(lineClasses);
            outWords.Add(lineWords);
        }

        return new CodeNotationResult(outLines, outClasses, outWords, containerClasses);
    }

    private static (string Content, bool Dropped) ProcessEffectsOrWord(
        string raw,
        List<(string[] Classes, int Remaining)> pendingClasses,
        List<(string Word, int Remaining)> pendingWords,
        HashSet<string> containerClasses,
        out (string[] Classes, int Count)? matchedHere,
        out (string Word, int Count)? wordMatchedHere)
    {
        matchedHere = null;
        wordMatchedHere = null;

        var effectsMatch = MarkerRegex.Match(raw);
        if (effectsMatch.Success)
        {
            var name = effectsMatch.Groups["name"].Value.ToLowerInvariant();
            var effect = Effects.FirstOrDefault(e => e.Name == name);
            if (effect.Classes is not null)
            {
                var count = ParseCount(effectsMatch);
                var prefix = effectsMatch.Groups["pre"].Value;

                if (effect.ContainerClass is not null)
                    containerClasses.Add(effect.ContainerClass);

                if (string.IsNullOrWhiteSpace(prefix))
                {
                    // Marker is the whole line: drop it, effect starts at the next emitted line.
                    pendingClasses.Add((effect.Classes, count));
                    return (string.Empty, true);
                }

                matchedHere = (effect.Classes, count);
                return (prefix.TrimEnd(), false);
            }
        }

        var wordMatch = WordMarkerRegex.Match(raw);
        if (wordMatch.Success)
        {
            var word = WordUnescapeRegex.Replace(wordMatch.Groups["word"].Value, "$1");
            var count = ParseCount(wordMatch);
            var prefix = wordMatch.Groups["pre"].Value;

            if (string.IsNullOrWhiteSpace(prefix))
            {
                pendingWords.Add((word, count));
                return (string.Empty, true);
            }

            wordMatchedHere = (word, count);
            return (prefix.TrimEnd(), false);
        }

        return (raw, false);
    }

    private static int ParseCount(Match match)
    {
        var countGroup = match.Groups["count"];
        return countGroup.Success && int.TryParse(countGroup.Value, out var count) ? count : 1;
    }

    private static HashSet<string> ApplyPendingClasses(
        List<(string[] Classes, int Remaining)> pending, (string[] Classes, int Count)? matchedHere)
    {
        var lineClasses = new HashSet<string>();

        foreach (var (classes, _) in pending)
            lineClasses.UnionWith(classes);
        DecrementPending(pending);

        if (matchedHere is { } m)
        {
            lineClasses.UnionWith(m.Classes);
            if (m.Count > 1)
                pending.Add((m.Classes, m.Count - 1));
        }

        return lineClasses;
    }

    private static HashSet<string> ApplyPendingWords(
        List<(string Word, int Remaining)> pending, (string Word, int Count)? matchedHere)
    {
        var lineWords = new HashSet<string>();

        foreach (var (word, _) in pending)
            lineWords.Add(word);
        DecrementPending(pending);

        if (matchedHere is { } m)
        {
            lineWords.Add(m.Word);
            if (m.Count > 1)
                pending.Add((m.Word, m.Count - 1));
        }

        return lineWords;
    }

    private static void DecrementPending<T>(List<(T Value, int Remaining)> pending)
    {
        for (var i = pending.Count - 1; i >= 0; i--)
        {
            var (value, remaining) = pending[i];
            remaining--;
            if (remaining <= 0)
                pending.RemoveAt(i);
            else
                pending[i] = (value, remaining);
        }
    }
}
