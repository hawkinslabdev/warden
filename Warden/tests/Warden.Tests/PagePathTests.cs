using Warden.Models;

namespace Warden.Tests;

public sealed class PagePathTests
{
    [Fact]
    public void FromFile_WithIndexMd_ReturnsDirectoryPath()
    {
        var path = PagePath.FromFile("getting-started/index.md");
        Assert.Equal("getting-started", path.Value);
    }

    [Fact]
    public void FromFile_WithRootIndexMd_ReturnsIndex()
    {
        var path = PagePath.FromFile("index.md");
        Assert.Equal("index", path.Value);
    }

    [Fact]
    public void FromFile_WithNonIndexMd_ReturnsPathWithoutExtension()
    {
        var path = PagePath.FromFile("getting-started/installation.md");
        Assert.Equal("getting-started/installation", path.Value);
    }

    [Fact]
    public void FromFile_NormalizesBackslashes()
    {
        var path = PagePath.FromFile("getting-started\\installation.md");
        Assert.Equal("getting-started/installation", path.Value);
    }

    [Fact]
    public void FromFile_LowercasesResult()
    {
        var path = PagePath.FromFile("Getting-Started/Installation.md");
        Assert.Equal("getting-started/installation", path.Value);
    }

    [Fact]
    public void Constructor_TrimsSlashes()
    {
        var path = new PagePath("/getting-started/installation/");
        Assert.Equal("getting-started/installation", path.Value);
    }

    [Fact]
    public void Constructor_LowercasesResult()
    {
        var path = new PagePath("Getting-Started/Installation");
        Assert.Equal("getting-started/installation", path.Value);
    }

    [Fact]
    public void Constructor_ThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(() => new PagePath(""));
        Assert.Throws<ArgumentException>(() => new PagePath("   "));
    }

    [Fact]
    public void Segments_SplitsPath()
    {
        var path = new PagePath("getting-started/installation");
        Assert.Equal(["getting-started", "installation"], path.Segments);
    }

    [Fact]
    public void Segments_SingleSegment()
    {
        var path = new PagePath("index");
        Assert.Equal(["index"], path.Segments);
    }

    [Fact]
    public void ToString_ReturnsValue()
    {
        var path = new PagePath("getting-started/installation");
        Assert.Equal("getting-started/installation", path.ToString());
    }

    [Fact]
    public void ImplicitConversion_ToString()
    {
        var path = new PagePath("getting-started/installation");
        string result = path;
        Assert.Equal("getting-started/installation", result);
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        var a = new PagePath("getting-started/installation");
        var b = new PagePath("getting-started/installation");
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_DifferentValue_AreNotEqual()
    {
        var a = new PagePath("getting-started/installation");
        var b = new PagePath("getting-started/configuration");
        Assert.NotEqual(a, b);
    }
}
