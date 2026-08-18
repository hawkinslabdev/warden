using Markdig;
using Markdig.Extensions.Emoji;
using Markdig.Extensions.Yaml;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Warden.Models;
using Warden.Services.MarkdownExtensions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Warden.Services;

public sealed partial class MarkdownService
{
    private const string BARK_LCB = "##BARK_LCB##";
    private const string BARK_RCB = "##BARK_RCB##";

    private readonly MarkdownPipeline _pipeline;
    private readonly IDeserializer _yamlDeserializer;
    private readonly string _basePath;
    private readonly ILogger<MarkdownService>? _logger;

    public MarkdownService(
        ISyntaxHighlighter? syntaxHighlighter = null,
        string basePath = "",
        CodeGroupIconOptions? codeGroupIcons = null,
        MathRenderer? mathRenderer = null,
        ILogger<MarkdownService>? logger = null)
    {
        _basePath = basePath;
        _logger = logger;
        _pipeline = new MarkdownPipelineBuilder()
            .UseAbbreviations()
            .UseAlertBlocks()
            .UseAutoIdentifiers()
            .UseAutoLinks()
            .UseCitations()
            .UseEmphasisExtras()
            .UseDefinitionLists()
            .UseEmojiAndSmiley(EmojiMapping.DefaultEmojisOnlyMapping)
            .UseFootnotes()
            .UseGridTables()
            .UseListExtras()
            .UseMediaLinks()
            .UseMathematics()
            .UsePipeTables()
            .UseTaskLists()
            .UseYamlFrontMatter()

            .UseMarkdownExtensions(syntaxHighlighter, codeGroupIcons, basePath, mathRenderer)

            // UseGenericAttributes() should be the last extension added to the pipeline, as it modifies other parsers to recognize attribute syntax (see https://xoofx.github.io/markdig/docs/extensions/generic-attributes)
            .UseGenericAttributes()
            .Build();

        _yamlDeserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .WithTypeConverter(new StringOrStringListTypeConverter())
            .Build();
    }

    // `keywords:`/`monitors:` are meant as lists, but a one-item `monitors: forgejo` is an easy typo to make
    // and previously blew up the whole file's front matter (see ParseFrontMatter's catch); accept either shape.
    private sealed class StringOrStringListTypeConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type) => type == typeof(List<string>);

        public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            if (parser.TryConsume<Scalar>(out var scalar))
                return new List<string> { scalar.Value };

            var list = new List<string>();
            parser.Consume<SequenceStart>();
            while (!parser.TryConsume<SequenceEnd>(out _))
                list.Add(parser.Consume<Scalar>().Value);
            return list;
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
            throw new NotSupportedException("Front matter is read-only.");
    }

    public MarkdownParseResult Parse(
        string markdown,
        string? defaultTitle = null,
        string? filePath = null)
    {
        markdown = EscapeBracesInCodeSpans(markdown);
        var document = Markdown.Parse(markdown, _pipeline);

        var frontMatter = ParseFrontMatter(document, filePath);

        var headings = new List<HeadingInfo>();
        foreach (var block in document.Descendants())
        {
            if (block is HeadingBlock heading)
            {
                var headingText = ExtractInlineText(heading.Inline);
                if (string.IsNullOrEmpty(headingText))
                    continue;

                var id = heading.GetAttributes().Id ?? Slugify(headingText);
                headings.Add(new HeadingInfo(headingText, id, heading.Level));
            }
        }

        var html = UnescapeBraces(AddHeadingAnchors(Markdown.ToHtml(markdown, _pipeline)));
        html = PrefixBodyContent(html, _basePath);
        html = AddExternalLinkAttributes(html);
        html = RewriteAbbreviations(html);
        html = AddLazyLoading(html);
        html = WrapImageFigures(html);

        var publishDate = frontMatter?.Date ?? frontMatter?.Start;

        return new MarkdownParseResult(
            html,
            frontMatter?.Title ?? defaultTitle,
            frontMatter?.Description is { Length: > 0 } d ? ToPlainText(d) : null,
            headings,
            frontMatter?.Layout,
            frontMatter?.LastUpdated ?? true,
            frontMatter?.Keywords is { Count: > 0 } kw ? kw.AsReadOnly() : null,
            frontMatter?.Pagination ?? true,
            frontMatter?.Redirect,
            frontMatter?.Updated ?? publishDate,
            publishDate,
            frontMatter?.Cover,
            frontMatter?.PageNext,
            frontMatter?.PagePrev ?? frontMatter?.PagePrevious,
            frontMatter?.Sitemap ?? true,
            frontMatter?.NoIndex ?? false,
            frontMatter?.Maintenance ?? false,
            frontMatter?.End,
            frontMatter?.Monitors is { Count: > 0 } mons ? mons.AsReadOnly() : null);
    }

    private static string AddHeadingAnchors(string html) =>
        HeadingRegex().Replace(html, match =>
        {
            var level = match.Groups[1].Value;
            var id = match.Groups[2].Value;
            var inner = match.Groups[3].Value;
            var plainText = TagRegex().Replace(inner, string.Empty);
            return $"<h{level} id=\"{id}\" tabindex=\"-1\">{inner} " +
                   $"<a class=\"header-anchor\" href=\"#{id}\" aria-label=\"Permalink to &quot;{plainText}&quot;\">&#8203;</a></h{level}>";
        });

    [GeneratedRegex(@"<h([2-6]) id=""([^""]+)"">(.*?)</h\1>", RegexOptions.Singleline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex TagRegex();

    private FrontMatter? ParseFrontMatter(MarkdownDocument document, string? filePath = null)
    {
        if (document.FirstOrDefault() is not YamlFrontMatterBlock yamlBlock)
            return null;

        var yaml = yamlBlock.Lines.ToString().Trim();
        if (string.IsNullOrWhiteSpace(yaml))
            return null;

        try
        {
            return _yamlDeserializer.Deserialize<FrontMatter>(yaml);
        }
        catch (YamlException ex)
        {
            _logger?.LogWarning(ex, "Malformed YAML frontmatter in {FilePath}, frontmatter will be ignored", filePath ?? "(unknown)");
            return null;
        }
    }

    private static string ExtractInlineText(ContainerInline? container)
    {
        if (container == null) return string.Empty;
        var sb = new StringBuilder();
        var inline = container.FirstChild;
        while (inline != null)
        {
            if (inline is LiteralInline lit)
                sb.Append(lit.Content);
            else if (inline is CodeInline code)
                sb.Append(code.Content);
            else if (inline is ContainerInline child)
                sb.Append(ExtractInlineText(child));
            inline = inline.NextSibling;
        }
        return sb.ToString().Trim();
    }

    public string ToHtml(string markdown) =>
        PrefixBodyContent(UnescapeBraces(Markdown.ToHtml(markdown, _pipeline)), _basePath);

    [GeneratedRegex(@"``[^`\n]*``|`[^`\n]*`")]
    private static partial Regex InlineCodeSpanRegex();

    private static string EscapeBracesInCodeSpans(string markdown) =>
        InlineCodeSpanRegex().Replace(markdown, m =>
            m.Value.Replace("{", BARK_LCB).Replace("}", BARK_RCB));

    private static string UnescapeBraces(string html) =>
        html.Replace(BARK_LCB, "{").Replace(BARK_RCB, "}");

    // Body content links/images: rewrite root-relative href/src to include basePath.
    private static string PrefixBodyContent(string html, string basePath)
    {
        if (string.IsNullOrEmpty(basePath))
            return html;

        return BodyContentUrlRegex().Replace(html, m =>
        {
            var url = m.Groups[2].Value;
            if (PrefixHasBasePath(url, basePath))
                return m.Value;
            return $"{m.Groups[1].Value}=\"{basePath}{url}\"";
        });
    }

    [GeneratedRegex(@"(href|src)=""(/(?!/)[^""]*)""")]
    private static partial Regex BodyContentUrlRegex();

    // External links (absolute http/https) open in a new tab safely, unless the author set target explicitly.
    private static string AddExternalLinkAttributes(string html) =>
        ExternalLinkRegex().Replace(html, m =>
        {
            var tag = m.Value;
            if (tag.Contains("target=", StringComparison.OrdinalIgnoreCase))
                return tag;
            var extra = tag.Contains("rel=", StringComparison.OrdinalIgnoreCase)
                ? " target=\"_blank\""
                : " target=\"_blank\" rel=\"noopener noreferrer\"";
            return string.Concat(tag.AsSpan(0, tag.Length - 1), extra, ">");
        });

    [GeneratedRegex(@"<a\s[^>]*href=""https?://[^""]*""[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalLinkRegex();

    // The browser's own abbr tooltip is hover-only, so touch never sees it, and it cannot be styled or
    // suppressed. The expansion moves to data-tip for the CSS tooltip, tabindex lets a tap open it, and
    // the visually hidden copy represents it to screen readers, which read a title attribute inconsistently.
    private static string RewriteAbbreviations(string html) =>
        AbbrTagRegex().Replace(html, m =>
            $"<abbr tabindex=\"0\" data-tip=\"{m.Groups[1].Value}\">{m.Groups[2].Value}" +
            $"<span class=\"sr-only\"> ({m.Groups[1].Value})</span></abbr>");

    [GeneratedRegex(@"<abbr title=""([^""]*)"">(.*?)</abbr>", RegexOptions.Singleline)]
    private static partial Regex AbbrTagRegex();

    // Content images default to lazy loading and async decoding unless the author set loading explicitly.
    private static string AddLazyLoading(string html) =>
        ImgTagRegex().Replace(html, m =>
            m.Value.Contains("loading=", StringComparison.OrdinalIgnoreCase)
                ? m.Value
                : m.Value.Insert(4, " loading=\"lazy\" decoding=\"async\""));

    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ImgTagRegex();

    private static string WrapImageFigures(string html) =>
        ParagraphImageRegex().Replace(html, m =>
        {
            var img = m.Groups[1].Value;
            var alt = ImgAltRegex().Match(img);
            return alt.Success && alt.Groups[1].Value.Length > 0
                ? $"<figure class=\"image-figure\">{img}<figcaption>{alt.Groups[1].Value}</figcaption></figure>"
                : m.Value;
        });

    [GeneratedRegex(@"<p>\s*(<img\b[^>]*>)\s*</p>", RegexOptions.IgnoreCase)]
    private static partial Regex ParagraphImageRegex();

    [GeneratedRegex(@"\balt=""([^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex ImgAltRegex();

    // Strips inline markup so frontmatter descriptions are searchable and safe for <meta name="description">.
    private static readonly MarkdownPipeline _plainTextPipeline = new MarkdownPipelineBuilder().Build();

    public static string ToPlainText(string markdown)
    {
        var html = Markdown.ToHtml(markdown, _plainTextPipeline);
        return WebUtility.HtmlDecode(TagRegex().Replace(html, string.Empty)).Trim();
    }

    private static bool PrefixHasBasePath(string path, string basePath)
    {
        if (string.IsNullOrEmpty(basePath))
            return false;
        return path.StartsWith(basePath, StringComparison.Ordinal)
            && (path.Length == basePath.Length || path[basePath.Length] == '/');
    }

    public static string Slugify(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
            {
                builder.Append(char.ToLowerInvariant(c));
            }
            else if (char.IsWhiteSpace(c) || c is '.' or ',' or '/' or '\\' or '+' or '~' or '#'
                     or '(' or ')' or '[' or ']' or '{' or '}' or '*' or '&' or '^' or '%'
                     or '$' or '!' or '@' or '`' or '\'' or '"' or ':' or ';' or '|'
                     or '<' or '>' or '?' or '=' or '~')
            {
                if (builder.Length > 0 && builder[^1] != '-')
                    builder.Append('-');
            }
        }

        while (builder.Length > 0 && builder[^1] == '-')
            builder.Length--;

        return builder.ToString();
    }
}
