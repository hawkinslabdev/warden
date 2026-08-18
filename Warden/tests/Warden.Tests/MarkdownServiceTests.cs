using Microsoft.Extensions.Logging;
using Warden.Services;
using Warden.Services.MarkdownExtensions;

namespace Warden.Tests;

/// <summary>Captures log calls for assertion in tests that need to verify logging behavior.</summary>
file sealed class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    IDisposable? ILogger.BeginScope<TState>(TState state) => null;
    bool ILogger.IsEnabled(LogLevel logLevel) => true;

    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));
}

public sealed class MarkdownServiceTests
{
    private readonly MarkdownService _service = new();

    [Fact]
    public void AllContent_ParseWithoutLeakingRawContainerOrFenceSyntax()
    {
        var contentDir = Path.Combine(AppContext.BaseDirectory, "content");
        if (!Directory.Exists(contentDir)) return;
        var problems = new List<string>();
        foreach (var file in Directory.GetFiles(contentDir, "*.md", SearchOption.AllDirectories))
        {
            var md = File.ReadAllText(file);
            var (html, _, _, _) = _service.Parse(md);
            if (html.Contains("<p>```") || html.Contains("<p>:::") || html.Contains("```</p>") || html.Contains(":::</p>"))
                problems.Add(file);
        }
        Assert.Empty(problems);
    }

    [Fact]
    public void Parse_WithFrontMatter_ExtractsTitle()
    {
        var md = "---\ntitle: My Custom Title\ndescription: A test page\n---\n\n# Content\n";
        var (_, title, description, _) = _service.Parse(md);
        Assert.Equal("My Custom Title", title);
        Assert.Equal("A test page", description);
    }

    [Fact]
    public void Parse_WithoutFrontMatter_UsesDefaultTitle()
    {
        var md = "# Just Content";
        var (_, title, _, _) = _service.Parse(md, "Default Title");
        Assert.Equal("Default Title", title);
    }

    [Fact]
    public void Parse_WithoutFrontMatter_DescriptionIsNull()
    {
        var md = "# Just Content";
        var (_, _, description, _) = _service.Parse(md);
        Assert.Null(description);
    }

    [Fact]
    public void Parse_GeneratesHtml()
    {
        var md = "# Hello";
        var (html, _, _, _) = _service.Parse(md);
        Assert.Contains("<h1", html);
        Assert.Contains("Hello", html);
    }

    [Fact]
    public void Parse_BoldEndingInColonAfterCode_IsNotEatenBySmileyParser()
    {
        var md = "**`docker run`:**";
        var (html, _, _, _) = _service.Parse(md);

        Assert.Contains("<strong>", html);
        Assert.DoesNotContain("\U0001F617", html);
    }

    [Fact]
    public void Parse_ShortcodeEmoji_StillRenders()
    {
        var md = ":tada:";
        var (html, _, _, _) = _service.Parse(md);

        Assert.Contains("\U0001F389", html);
    }

    [Fact]
    public void Parse_FencedBlockWithTitle_RendersTitleBarAndHidesLang()
    {
        var md = "```json [./appsettings.json]\n{\n  \"Hello\": \"world\"\n}\n```\n";
        var (html, _, _, _) = _service.Parse(md);

        Assert.Contains("class=\"language-json has-title\"", html);
        Assert.Contains("data-filename=\"./appsettings.json\"", html);
        Assert.Contains("<div class=\"code-title\">./appsettings.json</div>", html);
    }

    [Fact]
    public void Parse_CodeGroupChild_DoesNotRenderTitleBar()
    {
        var md = "::: code-group\n```json [./appsettings.json]\n{}\n```\n:::\n";
        var (html, _, _, _) = _service.Parse(md);

        Assert.DoesNotContain("code-title", html);
    }

    [Fact]
    public void Parse_FourBacktickFence_ToleratesNestedTripleBacktickFences()
    {
        // CommonMark closes a fence on any same/longer same-char line, so nested ``` needs a longer outer fence.
        var md = "````md\n```sh\necho hi\n```\n````\n\nAfter";
        var (html, _, _, _) = _service.Parse(md);
        Assert.Contains("```sh", html);
        Assert.Contains("After", html);
        Assert.DoesNotContain("<p>```", html);
    }

    [Fact]
    public void Parse_ExtractsHeadings()
    {
        var md = """
# Title
## Section One
### Sub Section
## Section Two
""";
        var (_, _, _, headings) = _service.Parse(md);
        Assert.Contains(headings, h => h.Text == "Section One");
        Assert.Contains(headings, h => h.Text == "Sub Section");
        Assert.Contains(headings, h => h.Text == "Section Two");
    }

    [Fact]
    public void Parse_HeadingsHaveSlugifiedIds()
    {
        var md = "## Hello World";
        var (_, _, _, headings) = _service.Parse(md);
        var heading = Assert.Single(headings);
        Assert.Equal("hello-world", heading.Id);
    }

    [Fact]
    public void Parse_EmptyMarkdown_ReturnsEmptyHtml()
    {
        var (html, _, _, _) = _service.Parse("");
        Assert.Empty(html);
    }

    [Fact]
    public void Parse_InvalidFrontMatter_DoesNotThrow()
    {
        var md = "---\ninvalid: : : yaml\n---\n\n# Content\n";
        var ex = Record.Exception(() => _service.Parse(md));
        Assert.Null(ex);
    }

    [Fact]
    public void Parse_CodeBlocksArePreserved()
    {
        var md = """
```csharp
var x = 1;
```
""";
        var (html, _, _, _) = _service.Parse(md);
        Assert.Contains("var x = 1", html);
    }

    [Fact]
    public void Parse_InlineMath_RendersStaticKaTeXHtml()
    {
        var service = new MarkdownService(mathRenderer: new MathRenderer());
        var (html, _, _, _) = service.Parse("$E = mc^2$");
        Assert.Contains("class=\"katex\"", html);
        Assert.Contains("annotation encoding=\"application/x-tex\">E = mc^2", html);
    }

    [Fact]
    public void Parse_BlockMath_RendersStaticKaTeXHtml()
    {
        var service = new MarkdownService(mathRenderer: new MathRenderer());
        var md = """
$$
E = mc^2
$$
""";
        var (html, _, _, _) = service.Parse(md);
        Assert.Contains("class=\"katex-display\"", html);
        Assert.Contains("E = mc^2", html);
    }

    [Fact]
    public void Slugify_ReplacesSpacesWithHyphens()
    {
        var result = MarkdownService.Slugify("Hello World");
        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void Slugify_RemovesSpecialChars()
    {
        var result = MarkdownService.Slugify("Hello, World! How's it going?");
        Assert.Equal("hello-world-how-s-it-going", result);
    }

    [Fact]
    public void Slugify_Lowercases()
    {
        var result = MarkdownService.Slugify("HELLO World");
        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void Slugify_CollapsesMultipleHyphens()
    {
        var result = MarkdownService.Slugify("Hello   World");
        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void Slugify_TrimsTrailingHyphens()
    {
        var result = MarkdownService.Slugify("Hello World!!!");
        Assert.Equal("hello-world", result);
    }

    [Fact]
    public void Slugify_EmptyString_ReturnsEmpty()
    {
        var result = MarkdownService.Slugify("");
        Assert.Equal("", result);
    }


    [Fact]
    public void Parse_PaginationFalse_ShowPaginationFalse()
    {
        var md = "---\npagination: false\n---\n\n# Content\n";
        var result = _service.Parse(md);
        Assert.False(result.ShowPagination);
    }

    [Fact]
    public void Parse_NoPaginationField_ShowPaginationTrue()
    {
        var md = "---\ntitle: Test\n---\n\n# Content\n";
        var result = _service.Parse(md);
        Assert.True(result.ShowPagination);
    }

    [Fact]
    public void Parse_WithRedirect_SetsRedirect()
    {
        var md = "---\nredirect: /guide/getting-started\n---\n";
        var result = _service.Parse(md);
        Assert.Equal("/guide/getting-started", result.Redirect);
    }

    [Fact]
    public void Parse_WithAbsoluteRedirect_SetsRedirect()
    {
        var md = "---\nredirect: https://example.com/new-page\n---\n";
        var result = _service.Parse(md);
        Assert.Equal("https://example.com/new-page", result.Redirect);
    }

    [Fact]
    public void Parse_NoRedirect_RedirectIsNull()
    {
        var md = "---\ntitle: Test\n---\n\n# Content\n";
        var result = _service.Parse(md);
        Assert.Null(result.Redirect);
    }

    [Fact]
    public void Parse_WithDate_SetsFrontmatterDate()
    {
        var md = "---\ndate: 2025-03-01\n---\n\n# Content\n";
        var result = _service.Parse(md);
        Assert.Equal(new DateTime(2025, 3, 1), result.FrontmatterDate);
    }

    [Fact]
    public void Parse_WithUpdated_SetsFrontmatterDate()
    {
        var md = "---\nupdated: 2025-06-28\n---\n\n# Content\n";
        var result = _service.Parse(md);
        Assert.Equal(new DateTime(2025, 6, 28), result.FrontmatterDate);
    }

    [Fact]
    public void Parse_WithBothDateAndUpdated_UpdatedTakesPriority()
    {
        var md = "---\ndate: 2025-01-01\nupdated: 2025-06-28\n---\n\n# Content\n";
        var result = _service.Parse(md);
        Assert.Equal(new DateTime(2025, 6, 28), result.FrontmatterDate);
    }

    [Fact]
    public void Parse_NoDateFields_FrontmatterDateIsNull()
    {
        var md = "---\ntitle: Test\n---\n\n# Content\n";
        var result = _service.Parse(md);
        Assert.Null(result.FrontmatterDate);
    }

    [Fact]
    public void Parse_WithStart_IsAliasForDate()
    {
        var md = "---\nstart: 2026-08-17T14:12:00Z\n---\n\n# Content\n";
        var result = _service.Parse(md);
        Assert.Equal(new DateTime(2026, 8, 17, 14, 12, 0), result.PublishDate);
    }

    [Fact]
    public void Parse_WithDateAndStart_DateTakesPriority()
    {
        var md = "---\ndate: 2026-01-01\nstart: 2026-08-17\n---\n\n# Content\n";
        var result = _service.Parse(md);
        Assert.Equal(new DateTime(2026, 1, 1), result.PublishDate);
    }

    [Fact]
    public void Parse_WithScalarMonitors_IsTreatedAsOneItemList()
    {
        var md = "---\nmaintenance: true\nmonitors: forgejo\n---\n\n# Content\n";
        var result = _service.Parse(md);
        Assert.Equal(["forgejo"], result.Monitors);
    }

    [Fact]
    public void Parse_WithListMonitors_ParsesNormally()
    {
        var md = "---\nmaintenance: true\nmonitors:\n  - forgejo\n  - codeberg\n---\n\n# Content\n";
        var result = _service.Parse(md);
        Assert.Equal(["forgejo", "codeberg"], result.Monitors);
    }

    [Fact]
    public void Parse_MalformedFrontMatter_LogsWarning()
    {
        var logger = new CapturingLogger<MarkdownService>();
        var service = new MarkdownService(logger: logger);
        var md = "---\ninvalid: : : yaml\n---\n\n# Content\n";

        service.Parse(md, filePath: "docs/test.md");

        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, logger.Entries[0].Level);
        Assert.Contains("docs/test.md", logger.Entries[0].Message);
    }

    [Fact]
    public void Parse_MalformedFrontMatter_StillReturnsHtml()
    {
        var logger = new CapturingLogger<MarkdownService>();
        var service = new MarkdownService(logger: logger);
        var md = "---\ninvalid: : : yaml\n---\n\n# Content after bad frontmatter\n";

        var result = service.Parse(md);

        Assert.Contains("Content after bad frontmatter", result.Html);
    }

    [Fact]
    public void Parse_MarkdownDescription_StripsToPlainText()
    {
        var md = "---\ndescription: See [Redirects](#redirects) for details\n---\n\n# Content\n";

        var result = _service.Parse(md);

        Assert.Equal("See Redirects for details", result.Description);
    }

    [Fact]
    public void Parse_BoldDescriptionMarkdown_StripsFormatting()
    {
        var md = "---\ndescription: \"**Important** feature overview\"\n---\n\n# Content\n";

        var result = _service.Parse(md);

        Assert.Equal("Important feature overview", result.Description);
    }

    [Fact]
    public void Parse_Abbreviation_RendersFocusableTagWithTooltipAndScreenReaderText()
    {
        var md = "The HTML spec.\n\n*[HTML]: Hyper Text Markup Language\n";

        var result = _service.Parse(md);

        Assert.Contains(
            "<abbr tabindex=\"0\" data-tip=\"Hyper Text Markup Language\">HTML" +
            "<span class=\"sr-only\"> (Hyper Text Markup Language)</span></abbr>",
            result.Html);
        Assert.DoesNotContain("<abbr title=", result.Html);
    }

    [Fact]
    public void ToPlainText_LinkMarkdown_ReturnsLinkText()
    {
        Assert.Equal("Redirects", MarkdownService.ToPlainText("[Redirects](#redirects)"));
    }

    [Fact]
    public void ToPlainText_BoldMarkdown_ReturnsPlainText()
    {
        Assert.Equal("hello world", MarkdownService.ToPlainText("**hello** world"));
    }

}
