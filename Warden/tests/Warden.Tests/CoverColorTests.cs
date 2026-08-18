using Warden.Services.Rendering;

namespace Warden.Tests;

public sealed class CoverColorTests
{
    [Theory]
    [InlineData("#abc")]
    [InlineData("#AABBCC")]
    [InlineData("#aabbccdd")]
    [InlineData("  #1a2b3c  ")]
    public void TryParse_AcceptsHex(string value)
    {
        Assert.True(CoverColor.TryParse(value, out var hex));
        Assert.StartsWith("#", hex);
        Assert.Equal(hex, hex.ToLowerInvariant());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/assets/cover.webp")]
    [InlineData("#12")]
    [InlineData("#12345")]
    [InlineData("#gggggg")]
    [InlineData("red")]
    public void TryParse_RejectsNonHex(string? value)
    {
        Assert.False(CoverColor.TryParse(value, out var hex));
        Assert.Equal(string.Empty, hex);
    }

    [Fact]
    public void InkFor_DarkBackground_YieldsLightInk()
    {
        var ink = CoverColor.InkFor("#101010");
        Assert.Equal("#f5f3ee", ink);
    }

    [Fact]
    public void InkFor_LightBackground_YieldsDarkInk()
    {
        var ink = CoverColor.InkFor("#f0f0f0");
        Assert.Equal("#14171a", ink);
    }

    [Fact]
    public void BuildCover_Hex_RendersColorBlockNotImg()
    {
        var html = PageChromeRenderer.BuildCover("#3a5f4a {.wide}", basePath: "");

        Assert.Contains("post-cover wide", html);
        Assert.Contains("background:#3a5f4a", html);
        Assert.DoesNotContain("<img", html);
    }

    [Fact]
    public void BuildCover_Url_StillRendersImg()
    {
        var html = PageChromeRenderer.BuildCover("/assets/cover.webp", basePath: "");

        Assert.Contains("<img", html);
        Assert.Contains("/assets/cover.webp", html);
    }
}
