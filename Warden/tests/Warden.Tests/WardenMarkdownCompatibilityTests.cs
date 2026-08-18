using Warden.Services;
using Warden.Services.MarkdownExtensions;

namespace Warden.Tests;

public sealed class WardenMarkdownCompatibilityTests
{
    private readonly MarkdownService _service = new();

    [Theory]
    [InlineData("tip", "TIP")]
    [InlineData("info", "INFO")]
    [InlineData("warning", "WARNING")]
    [InlineData("danger", "DANGER")]
    public void Container_WithDefaultTitle_RendersCustomBlock(string klass, string defaultTitle)
    {
        var md = $"::: {klass}\nSome content\n:::\n";
        var (html, _, _, _) = _service.Parse(md);

        Assert.Contains($"class=\"{klass} custom-block\"", html);
        Assert.Contains("custom-block-title custom-block-title-default", html);
        Assert.Contains(defaultTitle, html);
        Assert.Contains("Some content", html);
    }

    [Fact]
    public void Container_WithCustomTitle_OverridesDefaultTitle()
    {
        var md = "::: tip Custom Title Here\nContent\n:::\n";
        var (html, _, _, _) = _service.Parse(md);

        Assert.Contains("Custom Title Here", html);
        Assert.DoesNotContain("custom-block-title-default", html);
    }

    [Fact]
    public void Container_Details_RendersDetailsSummary()
    {
        var md = "::: details\nHidden content\n:::\n";
        var (html, _, _, _) = _service.Parse(md);

        Assert.Contains("<details class=\"details custom-block\">", html);
        Assert.Contains("<summary>Details</summary>", html);
        Assert.Contains("Hidden content", html);
    }

    [Fact]
    public void CodeGroup_RendersTabsWithTitlesAndActiveFirst()
    {
        var md = """
::: code-group
```js [config.js]
module.exports = {}
```
```ts [config.ts]
export default {}
```
:::
""";
        var (html, _, _, _) = _service.Parse(md);

        Assert.Contains("warden-code-group", html);
        Assert.Contains("data-title=\"config.js\"", html);
        Assert.Contains("data-title=\"config.ts\"", html);
        Assert.Contains("language-js active", html);
        Assert.DoesNotContain("language-ts active", html);
    }

    [Fact]
    public void FencedCode_MetaHighlightRange_MarksLinesHighlighted()
    {
        var md = """
```js{1,3}
line1
line2
line3
line4
```
""";
        var (html, _, _, _) = _service.Parse(md);

        var lines = ExtractLineSpans(html);
        Assert.Contains("highlighted", lines[0]);
        Assert.DoesNotContain("highlighted", lines[1]);
        Assert.Contains("highlighted", lines[2]);
        Assert.DoesNotContain("highlighted", lines[3]);
    }

    [Fact]
    public void FencedCode_TrailingHighlightComment_StripsMarkerAndHighlightsLine()
    {
        var md = """
```js
const a = 1 // [!code highlight]
const b = 2
```
""";
        var (html, _, _, _) = _service.Parse(md);

        var lines = ExtractLineSpans(html);
        Assert.Contains("highlighted", lines[0]);
        Assert.Contains("const a = 1", lines[0]);
        Assert.DoesNotContain("[!code", lines[0]);
        Assert.DoesNotContain("highlighted", lines[1]);
    }

    [Fact]
    public void FencedCode_StandaloneHighlightComment_DropsLineAndHighlightsNext()
    {
        var md = """
```js
// [!code highlight]
const a = 1
const b = 2
```
""";
        var (html, _, _, _) = _service.Parse(md);

        var lines = ExtractLineSpans(html);
        Assert.Equal(2, lines.Count);
        Assert.Contains("highlighted", lines[0]);
        Assert.Contains("const a = 1", lines[0]);
        Assert.DoesNotContain("highlighted", lines[1]);
    }

    [Fact]
    public void FencedCode_DiffNotation_AddsAndRemovesClasses()
    {
        var md = """
```js
const a = 1 // [!code --]
const b = 2 // [!code ++]
```
""";
        var (html, _, _, _) = _service.Parse(md);
        var lines = ExtractLineSpans(html);

        Assert.Contains("diff remove", lines[0]);
        Assert.Contains("diff add", lines[1]);
        Assert.Contains("has-diff", html);
    }

    [Fact]
    public void FencedCode_FocusNotation_UsesOverrideClasses()
    {
        var md = """
```js
const a = 1 // [!code focus]
const b = 2
```
""";
        var (html, _, _, _) = _service.Parse(md);
        var lines = ExtractLineSpans(html);

        Assert.Contains("has-focus", lines[0]);
        Assert.Contains("has-focused-lines", html);
    }

    [Theory]
    [InlineData("error", "error")]
    [InlineData("warning", "warning")]
    public void FencedCode_ErrorLevelNotation_AddsHighlightedAndLevelClass(string marker, string expectedClass)
    {
        var md = $"""
```js
const a = 1 // [!code {marker}]
```
""";
        var (html, _, _, _) = _service.Parse(md);
        var lines = ExtractLineSpans(html);

        Assert.Contains("highlighted", lines[0]);
        Assert.Contains(expectedClass, lines[0]);
        Assert.Contains("has-highlighted", html);
    }

    [Fact]
    public void FencedCode_LineNumbersFlag_RendersGutter()
    {
        var md = """
```js:line-numbers
const a = 1
const b = 2
```
""";
        var (html, _, _, _) = _service.Parse(md);

        Assert.Contains("line-numbers-mode", html);
        Assert.Contains("line-numbers-wrapper", html);
        Assert.Contains("<span class=\"line-number\">1</span>", html);
        Assert.Contains("<span class=\"line-number\">2</span>", html);
    }

    [Fact]
    public void FencedCode_LineNumbersStart_OffsetsFromGivenNumber()
    {
        var md = """
```js:line-numbers=10
const a = 1
```
""";
        var (html, _, _, _) = _service.Parse(md);

        Assert.Contains("<span class=\"line-number\">10</span>", html);
    }

    [Fact]
    public void FencedCode_PlainCodeBlock_RendersLanguageWrapperAndCopyButton()
    {
        var md = """
```csharp
var x = 1;
```
""";
        var (html, _, _, _) = _service.Parse(md);

        Assert.Contains("class=\"language-csharp\"", html);
        Assert.Contains("class=\"copy\"", html);
        Assert.Contains("class=\"lang\">csharp</span>", html);
        Assert.Contains("var x = 1;", html);
    }

    [Fact]
    public void Math_BlockAndInline_RenderToStaticKaTeXHtml()
    {
        var service = new MarkdownService(mathRenderer: new MathRenderer());
        var md = "Inline $x^2$ and block:\n\n$$\nE = mc^2\n$$\n";
        var (html, _, _, _) = service.Parse(md);

        Assert.Contains("class=\"katex\"", html);
        Assert.Contains("class=\"katex-display\"", html);
        Assert.Contains("annotation encoding=\"application/x-tex\">x^2", html);
        Assert.Contains("E = mc^2", html);
    }

    [Fact]
    public void CodeBlockMeta_ParsesCombinedLangTitleAndLineNumbers()
    {
        var meta = CodeBlockMeta.Parse("js{2-3}:line-numbers=5", "[app.js]");

        Assert.Equal("js", meta.Lang);
        Assert.Equal("app.js", meta.Title);
        Assert.True(meta.LineNumbers);
        Assert.Equal(5, meta.LineNumbersStart);
        Assert.Equal(new[] { 2, 3 }, meta.HighlightedLines.OrderBy(x => x));
    }

    [Fact]
    public void CodeBlockMeta_VueSuffix_StripsAndMapsLang()
    {
        Assert.Equal("html", CodeBlockMeta.Parse("html-vue", null).Lang);
        Assert.Equal("template", CodeBlockMeta.Parse("vue-html", null).Lang);
        Assert.Equal(string.Empty, CodeBlockMeta.Parse("ansi", null).Lang);
    }

    [Fact]
    public void CodeBlockMeta_ParsesWordHighlightSlashSyntax()
    {
        var meta = CodeBlockMeta.Parse("js", "/hello/ /world/");
        Assert.Equal(["hello", "world"], meta.WordHighlights);
    }

    [Fact]
    public void WordHighlight_MetaSlashSyntax_WrapsMatchedSubstring()
    {
        var md = """
```js /world/
hello world
```
""";
        var (html, _, _, _) = _service.Parse(md);

        Assert.Contains("<span class=\"highlighted-word\">world</span>", html);
        Assert.Contains("hello ", html);
    }

    [Fact]
    public void WordHighlight_TrailingNotationComment_StripsMarkerAndHighlightsWord()
    {
        var md = """
```js
const apple = 1 // [!code word:apple]
```
""";
        var (html, _, _, _) = _service.Parse(md);

        Assert.Contains("<span class=\"highlighted-word\">apple</span>", html);
        Assert.DoesNotContain("[!code", html);
    }

    [Fact]
    public void WordHighlight_StandaloneNotationComment_DropsLineAndHighlightsNextLine()
    {
        var md = """
```js
// [!code word:apple]
const apple = 1
```
""";
        var (html, _, _, _) = _service.Parse(md);
        var lines = ExtractLineSpans(html);

        Assert.Equal(1, lines.Count(l => l.Contains("const")));
        Assert.Contains("<span class=\"highlighted-word\">apple</span>", html);
    }

    [Fact]
    public async Task TextMateSyntaxHighlighter_TokenizesJavaScript_WithGithubThemeColors()
    {
        var highlighter = new TextMateSyntaxHighlighter();
        await highlighter.InitializeAsync(CancellationToken.None);
        var service = new MarkdownService(highlighter);

        var md = """
```js
const x = 1;
```
""";
        var (html, _, _, _) = service.Parse(md);

        Assert.Contains("shiki shiki-themes github-light github-dark", html);
        Assert.Contains("--shiki-light:#", html);
        Assert.Contains("--shiki-dark:#", html);
        Assert.Contains("const", html);
    }

    [Fact]
    public async Task TextMateSyntaxHighlighter_TokenizesCSharp_KeywordsGetDistinctColorFromIdentifiers()
    {
        var highlighter = new TextMateSyntaxHighlighter();
        await highlighter.InitializeAsync(CancellationToken.None);

        var lines = highlighter.TokenizeLines(["var x = 1;"], "csharp");
        var tokens = lines[0];

        Assert.Contains(tokens, t => t.Text == "var");
        Assert.Contains(tokens, t => t.Text == "x");

        var keywordColor = tokens.First(t => t.Text == "var").LightColor;
        var identifierColor = tokens.First(t => t.Text == "x").LightColor;
        Assert.NotEqual(keywordColor, identifierColor);
    }

    [Fact]
    public async Task TextMateSyntaxHighlighter_UnknownLanguage_FallsBackToPlainTokens()
    {
        var highlighter = new TextMateSyntaxHighlighter();
        await highlighter.InitializeAsync(CancellationToken.None);

        var lines = highlighter.TokenizeLines(["some text"], "not-a-real-language-xyz");

        var token = Assert.Single(lines[0]);
        Assert.Equal("some text", token.Text);
        Assert.Null(token.LightColor);
        Assert.Null(token.DarkColor);
    }

    [Fact]
    public async Task TextMateSyntaxHighlighter_MultiLineBlockComment_CarriesStateAcrossLines()
    {
        var highlighter = new TextMateSyntaxHighlighter();
        await highlighter.InitializeAsync(CancellationToken.None);

        var lines = highlighter.TokenizeLines(["/* start", "still a comment", "end */ var x = 1;"], "csharp");

        // One comment color across all three lines proves the rule stack represents between TokenizeLines calls.
        var line1Color = lines[1][0].LightColor;
        var commentTailToken = lines[2].First(t => t.Text.Contains("end"));

        Assert.Equal(line1Color, commentTailToken.LightColor);
    }

    [Fact]
    public void Parse_NonHomeLayout_NoHeroMarkup()
    {
        var result = _service.Parse("# Just a normal page\n");

        Assert.Null(result.Layout);
        Assert.DoesNotContain("warden-home", result.Html);
    }

    [Fact]
    public void Parse_LastUpdated_DefaultsTrue_OverridableFalse()
    {
        var defaultResult = _service.Parse("# Page\n");
        Assert.True(defaultResult.ShowLastUpdated);

        var overriddenResult = _service.Parse("---\nlastUpdated: false\n---\n\n# Page\n");
        Assert.False(overriddenResult.ShowLastUpdated);
    }

    private static List<string> ExtractLineSpans(string html)
    {
        var start = html.IndexOf("<code>", StringComparison.Ordinal) + "<code>".Length;
        var end = html.IndexOf("</code>", StringComparison.Ordinal);
        var inner = html[start..end];
        return inner.Split('\n').Where(l => l.Length > 0).ToList();
    }
}
