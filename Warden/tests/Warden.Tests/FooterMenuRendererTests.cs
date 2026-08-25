using Warden.Models;
using Warden.Services.Rendering;

namespace Warden.Tests;

public sealed class FooterMenuRendererTests
{
    [Fact]
    public void NullMenu_ReturnsEmpty()
    {
        Assert.Equal("", FooterMenuRenderer.Build(null, basePath: ""));
    }

    [Fact]
    public void DeclaredEmptyMenu_ReturnsEmpty()
    {
        var html = FooterMenuRenderer.Build([], basePath: "");

        Assert.Equal("", html);
    }

    [Fact]
    public void PopulatedMenu_RendersLinks()
    {
        var html = FooterMenuRenderer.Build(
            [new MenuLink { Title = "GitHub", Path = "https://github.com/hawkinslabdev/warden", External = true }],
            basePath: "");

        Assert.Equal("<a href=\"https://github.com/hawkinslabdev/warden\" target=\"_blank\" rel=\"noopener noreferrer\">GitHub</a>", html);
    }
}
