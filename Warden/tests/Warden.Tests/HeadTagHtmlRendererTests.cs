using Warden.Models;
using Warden.Services.Rendering;

namespace Warden.Tests;

public sealed class HeadTagHtmlRendererTests
{
    [Fact]
    public void BuildHeadTagsHtml_NullList_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, HeadTagHtmlRenderer.BuildHeadTagsHtml(null));
    }

    [Fact]
    public void BuildHeadTagsHtml_EmptyList_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, HeadTagHtmlRenderer.BuildHeadTagsHtml([]));
    }

    [Fact]
    public void BuildHeadTagsHtml_VoidMeta_NoClosingTag()
    {
        var tags = new List<HeadTag>
        {
            new() { Tag = "meta", Attrs = new() { ["name"] = "robots", ["content"] = "noindex" } }
        };
        var html = HeadTagHtmlRenderer.BuildHeadTagsHtml(tags);

        Assert.Contains("<meta", html);
        Assert.Contains("name=\"robots\"", html);
        Assert.Contains("content=\"noindex\"", html);
        Assert.DoesNotContain("</meta>", html);
    }

    [Fact]
    public void BuildHeadTagsHtml_VoidLink_NoClosingTag()
    {
        var tags = new List<HeadTag>
        {
            new() { Tag = "link", Attrs = new() { ["rel"] = "canonical", ["href"] = "https://example.com/" } }
        };
        var html = HeadTagHtmlRenderer.BuildHeadTagsHtml(tags);

        Assert.Contains("<link", html);
        Assert.DoesNotContain("</link>", html);
    }

    [Fact]
    public void BuildHeadTagsHtml_ScriptTag_HasContentAndClosingTag()
    {
        var tags = new List<HeadTag>
        {
            new() { Tag = "script", Attrs = new() { ["type"] = "application/ld+json" }, Content = "{\"@type\":\"WebSite\"}" }
        };
        var html = HeadTagHtmlRenderer.BuildHeadTagsHtml(tags);

        Assert.Contains("<script", html);
        Assert.Contains("{\"@type\":\"WebSite\"}", html);
        Assert.Contains("</script>", html);
    }

    [Fact]
    public void BuildHeadTagsHtml_AttrsValues_HtmlEncoded()
    {
        var tags = new List<HeadTag>
        {
            new() { Tag = "meta", Attrs = new() { ["content"] = "<evil>&\"" } }
        };
        var html = HeadTagHtmlRenderer.BuildHeadTagsHtml(tags);

        Assert.Contains("&lt;evil&gt;&amp;&quot;", html);
        Assert.DoesNotContain("<evil>", html);
    }

    [Fact]
    public void BuildHeadTagsHtml_TagWithEmptyName_Skipped()
    {
        var tags = new List<HeadTag>
        {
            new() { Tag = "" },
            new() { Tag = "meta", Attrs = new() { ["name"] = "author" } }
        };
        var html = HeadTagHtmlRenderer.BuildHeadTagsHtml(tags);

        Assert.Contains("<meta", html);
        Assert.DoesNotContain("<>", html);
    }

    [Fact]
    public void BuildHeadTagsHtml_NoAttrs_RendersTagOnly()
    {
        var tags = new List<HeadTag> { new() { Tag = "meta" } };
        var html = HeadTagHtmlRenderer.BuildHeadTagsHtml(tags);

        Assert.Contains("<meta>", html);
    }

    [Fact]
    public void BuildHeadTagsHtml_CaseInsensitiveVoidDetection()
    {
        var tags = new List<HeadTag> { new() { Tag = "META" } };
        var html = HeadTagHtmlRenderer.BuildHeadTagsHtml(tags);

        Assert.DoesNotContain("</META>", html);
    }
}
