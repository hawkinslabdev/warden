using Warden.Models;
using Warden.Services;
using Warden.Services.Rendering;

namespace Warden.Tests;

public sealed class FooterRendererTests
{
    private static readonly MarkdownService Markdown = new();

    [Fact]
    public void NoConfig_OmitsFooterEntirely()
    {
        var html = FooterRenderer.Build(null, basePath: "", brandText: "Warden", Markdown);

        Assert.Equal("", html);
    }

    [Fact]
    public void DeclaredEmptyMenu_NoFooterText_OmitsFooterEntirely()
    {
        var config = new Config { FooterMenu = [] };

        var html = FooterRenderer.Build(config, basePath: "", brandText: "Warden", Markdown);

        Assert.Equal("", html);
    }

    [Fact]
    public void DeclaredEmptyMenu_WithCustomFooterText_StillRenders()
    {
        var config = new Config { FooterMenu = [], Footer = "All rights reserved." };

        var html = FooterRenderer.Build(config, basePath: "", brandText: "Warden", Markdown);

        Assert.Contains("site-footer", html);
        Assert.Contains("All rights reserved.", html);
    }

    [Fact]
    public void PopulatedMenu_Renders()
    {
        var config = new Config { FooterMenu = [new MenuLink { Title = "Privacy", Path = "/privacy/" }] };

        var html = FooterRenderer.Build(config, basePath: "", brandText: "Warden", Markdown);

        Assert.Contains("site-footer", html);
        Assert.Contains("Privacy", html);
    }
}
