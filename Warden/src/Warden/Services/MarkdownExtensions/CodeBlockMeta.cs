using System.Text.RegularExpressions;

namespace Warden.Services.MarkdownExtensions;

/// <summary>Parsed fenced code block meta: langauge, tab title, line-numbers toggle, `{1,3-5}` highlighted ranges</summary>
public sealed record CodeBlockMeta(
    string Lang,
    string? Title,
    string? IconSlug,
    bool? LineNumbers,
    int? LineNumbersStart,
    IReadOnlySet<int> HighlightedLines,
    IReadOnlyList<string> WordHighlights)
{
    // '#' and '+' are part of the name: without them c#/f#/c++ truncate to the c and f grammars.
    private static readonly Regex LangRegex = new(@"^[a-zA-Z0-9_+#.-]+", RegexOptions.Compiled);
    private static readonly Regex TitleRegex = new(@"\[(.*)\]", RegexOptions.Compiled);
    private static readonly Regex TitleStripRegex = new(@"\[.*\]", RegexOptions.Compiled);
    private static readonly Regex IconRegex = new(@"\s+icon:(\S+)$", RegexOptions.Compiled);

    private static readonly Regex LineNumbersOnRegex = new(@":line-numbers\b", RegexOptions.Compiled);
    private static readonly Regex LineNumbersOffRegex = new(@":no-line-numbers\b", RegexOptions.Compiled);
    private static readonly Regex LineNumbersStartRegex = new(@"=(\d+)", RegexOptions.Compiled);
    private static readonly Regex HighlightRangeRegex = new(@"\{([\d,-]+)\}", RegexOptions.Compiled);
    private static readonly Regex WordHighlightRegex = new(@"/((?:\\.|[^/])+)/", RegexOptions.Compiled);
    private static readonly Regex WordUnescapeRegex = new(@"\\(.)", RegexOptions.Compiled);

    /// <summary>Recombines Markdig's split <c>Info</c>/<c>Arguments</c> back into one info-string before parsing.</summary>
    public static CodeBlockMeta Parse(string? info, string? arguments)
    {
        var combined = CombineInfoString(info, arguments);

        string? title = null;
        string? iconSlug = null;
        var titleMatch = TitleRegex.Match(combined);
        if (titleMatch.Success)
        {
            title = titleMatch.Groups[1].Value;
            var iconMatch = IconRegex.Match(title);
            if (iconMatch.Success)
            {
                iconSlug = iconMatch.Groups[1].Value;
                title = IconRegex.Replace(title, string.Empty);
            }
            combined = TitleStripRegex.Replace(combined, string.Empty);
        }

        bool? lineNumbers = null;
        int? lineNumbersStart = null;
        if (LineNumbersOnRegex.IsMatch(combined))
        {
            lineNumbers = true;
            var startMatch = LineNumbersStartRegex.Match(combined);
            if (startMatch.Success)
                lineNumbersStart = int.Parse(startMatch.Groups[1].Value);
        }
        else if (LineNumbersOffRegex.IsMatch(combined))
        {
            lineNumbers = false;
        }

        var highlighted = ParseHighlightRanges(combined);
        var wordHighlights = ParseWordHighlights(combined);
        var lang = ExtractLang(combined);

        return new CodeBlockMeta(lang, title, iconSlug, lineNumbers, lineNumbersStart, highlighted, wordHighlights);
    }

    private static string CombineInfoString(string? info, string? arguments)
    {
        if (string.IsNullOrEmpty(info))
            return arguments ?? string.Empty;

        return string.IsNullOrEmpty(arguments) ? info : $"{info} {arguments}";
    }

    private static HashSet<int> ParseHighlightRanges(string combined)
    {
        var result = new HashSet<int>();
        var match = HighlightRangeRegex.Match(combined);
        if (!match.Success)
            return result;

        foreach (var part in match.Groups[1].Value.Split(','))
        {
            var bounds = part.Split('-');
            if (bounds.Length == 1)
            {
                if (int.TryParse(bounds[0], out var single))
                    result.Add(single);
            }
            else if (bounds.Length == 2 &&
                     int.TryParse(bounds[0], out var start) &&
                     int.TryParse(bounds[1], out var end))
            {
                for (var i = start; i <= end; i++)
                    result.Add(i);
            }
        }

        return result;
    }

    private static List<string> ParseWordHighlights(string combined)
    {
        var words = new List<string>();
        foreach (Match match in WordHighlightRegex.Matches(combined))
            words.Add(WordUnescapeRegex.Replace(match.Groups[1].Value, "$1"));
        return words;
    }

    private static string ExtractLang(string combined)
    {
        var match = LangRegex.Match(combined);
        if (!match.Success)
            return string.Empty;

        var lang = match.Value.ToLowerInvariant();

        if (lang.EndsWith("-vue", StringComparison.Ordinal))
            lang = lang[..^4];
        else if (lang == "vue-html")
            lang = "template";
        else if (lang == "ansi")
            lang = string.Empty;

        return lang;
    }
}
