using Warden.Models;
using Warden.Services;

namespace Warden.Tests;

public sealed class ContainerRendererTests
{
    private const string CodeGroupMd = "::: code-group\n```sh [npm]\nnpm install\n```\n```sh [pnpm]\npnpm install\n```\n:::\n";

    [Fact]
    public void CodeGroup_IconsEnabledByDefault_RendersIconWithSlugifiedTitle()
    {
        var options = new CodeGroupIconOptions();
        var service = new MarkdownService(codeGroupIcons: options);
        var (html, _, _, _) = service.Parse(CodeGroupMd);

        Assert.Contains($"{options.BaseUrl}/npm.{options.Format}", html);
        Assert.Contains($"{options.BaseUrl}/pnpm.{options.Format}", html);
    }

    [Fact]
    public void CodeGroup_IconsDisabled_NoTabIcon()
    {
        var options = new CodeGroupIconOptions { Enabled = false };
        var service = new MarkdownService(codeGroupIcons: options);
        var (html, _, _, _) = service.Parse(CodeGroupMd);
        Assert.DoesNotContain("tab-icon", html);
    }

    [Fact]
    public void CodeGroup_OverrideWinsOverSlugifiedTitle()
    {
        var options = new CodeGroupIconOptions
        {
            Overrides = new Dictionary<string, string> { ["npm"] = "nodedotjs" }
        };
        var service = new MarkdownService(codeGroupIcons: options);
        var (html, _, _, _) = service.Parse(CodeGroupMd);

        Assert.Contains($"{options.BaseUrl}/nodedotjs.{options.Format}", html);
    }

    [Fact]
    public void CodeGroup_InlineIconDirective_WinsOverSlugifiedTitleAndGlobalOverride()
    {
        var options = new CodeGroupIconOptions
        {
            Overrides = new Dictionary<string, string> { ["csharp"] = "sharp" }
        };
        var service = new MarkdownService(codeGroupIcons: options);
        var md = "::: code-group\n```sh [csharp icon:dotnet]\ndotnet run\n```\n:::\n";
        var (html, _, _, _) = service.Parse(md);

        Assert.Contains($"{options.BaseUrl}/dotnet.{options.Format}", html);
        Assert.DoesNotContain($"{options.BaseUrl}/sharp.{options.Format}", html);
        Assert.Contains("data-title=\"csharp\"", html);
    }
}
