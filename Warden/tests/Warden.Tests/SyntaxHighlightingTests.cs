using Warden.Services;
using Warden.Services.MarkdownExtensions;

namespace Warden.Tests;

public sealed class SyntaxHighlightingTests
{
    private static readonly MarkdownService Service = CreateService();

    private static MarkdownService CreateService()
    {
        var highlighter = new TextMateSyntaxHighlighter();
        highlighter.InitializeAsync(CancellationToken.None).GetAwaiter().GetResult();
        return new MarkdownService(highlighter);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("c#")]
    [InlineData("cs")]
    [InlineData("dotnet")]
    [InlineData("fsharp")]
    [InlineData("f#")]
    [InlineData("cpp")]
    [InlineData("c++")]
    [InlineData("kotlin")]
    [InlineData("toml")]
    [InlineData("terraform")]
    [InlineData("yaml")]
    [InlineData("yml")]
    [InlineData("json")]
    [InlineData("jsonb")]
    [InlineData("xml")]
    [InlineData("shell")]
    [InlineData("console")]
    [InlineData("tsql")]
    [InlineData("dotenv")]
    [InlineData("powershell")]
    [InlineData("pwsh")]
    public void Fence_KnownLanguage_ProducesColoredTokens(string lang)
    {
        var html = Service.ToHtml($"```{lang}\nvar x = \"hello\";\n```\n");

        Assert.True(
            html.Split("--shiki-light:").Length > 2,
            $"{lang}: no per-token colors, only the <pre> default. HTML: {html}");
    }

    // The lang name reaches the class and the badge intact; a truncated "c" would also mean the C grammar.
    [Theory]
    [InlineData("c#")]
    [InlineData("f#")]
    [InlineData("c++")]
    public void Fence_LanguageWithSymbol_KeepsFullName(string lang)
    {
        var html = Service.ToHtml($"```{lang}\nvar x = 1;\n```\n");

        Assert.Contains($"class=\"language-{lang}\"", html);
    }

    [Fact]
    public void Fence_PlainShellArguments_UseDefaultForegroundNotStringColor()
    {
        var html = Service.ToHtml("```bash\ncurl --url 'https://x'\n```\n");

        // github-dark colors strings #9ECBFF; only the quoted URL may carry it.
        Assert.Contains("--shiki-dark:#e1e4e8\">curl --url ", html);
        Assert.Contains("#9ECBFF\">https://x<", html);
    }
}
