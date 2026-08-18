using Warden.Services;
using Warden.Services.Layout;

namespace Warden.Tests;

public sealed class LayoutProviderTests
{
    [Fact]
    public void GetLayout_ContainsTitle()
    {
        var html = LayoutProvider.GetLayout(
            "Test Title", "<p>content</p>",
            null);

        Assert.Contains("Test Title", html);
    }

    [Fact]
    public void GetLayout_ContainsContent()
    {
        var html = LayoutProvider.GetLayout(
            "Title", "<p>Hello World</p>",
            null);

        Assert.Contains("Hello World", html);
    }

    [Fact]
    public void GetLayout_ContainsThemeCss_WhenProvided()
    {
        var themeCss = "<style>:root { --primary-color: red; }</style>";
        var html = LayoutProvider.GetLayout(
            "Title", "<p>content</p>",
            themeCss);

        Assert.Contains("--primary-color: red;", html);
    }

    [Fact]
    public void GetLayout_HtmlEncodesTitle()
    {
        var html = LayoutProvider.GetLayout(
            "Test <script>alert('xss')</script>", "<p>content</p>",
            null);

        Assert.Contains("Test &lt;script&gt;", html);
        Assert.Contains("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;", html);
    }

    [Fact]
    public void GetLayout_HtmlEncodesDescription()
    {
        var html = LayoutProvider.GetLayout(
            "Title", "<p>content</p>",
            null,
            description: "desc <script>alert('xss')</script>");

        Assert.Contains("desc &lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>alert('xss')</script>", html);
    }

    [Fact]
    public void GetLayout_DefaultLang_IsEn()
    {
        var html = LayoutProvider.GetLayout(
            "Title", "<p>content</p>",
            null);

        Assert.Contains("lang=\"en\"", html);
    }

    [Fact]
    public void GetLayout_CustomLang_UsedInHtmlTag()
    {
        var html = LayoutProvider.GetLayout(
            "Title", "<p>content</p>",
            null,
            lang: "fr");

        Assert.Contains("lang=\"fr\"", html);
        Assert.DoesNotContain("lang=\"en\"", html);
    }

    [Fact]
    public void GetLayout_HeadTagsHtml_InjectedInHead()
    {
        var html = LayoutProvider.GetLayout(
            "Title", "<p>content</p>",
            null,
            headTagsHtml: "<meta name=\"robots\" content=\"noindex\">");

        Assert.Contains("<meta name=\"robots\" content=\"noindex\">", html);
        var headEnd = html.IndexOf("</head>", StringComparison.Ordinal);
        var metaPos = html.IndexOf("<meta name=\"robots\"", StringComparison.Ordinal);
        Assert.True(metaPos < headEnd, "head tag should appear before </head>");
    }

    [Fact]
    public void Get404Layout_DefaultLang_IsEn()
    {
        var html = LayoutProvider.Get404Layout(LayoutProvider.HtmlEncode);
        Assert.Contains("lang=\"en\"", html);
    }

    [Fact]
    public void Get404Layout_CustomLang_UsedInHtmlTag()
    {
        var html = LayoutProvider.Get404Layout(LayoutProvider.HtmlEncode, lang: "de");
        Assert.Contains("lang=\"de\"", html);
        Assert.DoesNotContain("lang=\"en\"", html);
    }

    [Fact]
    public void Get404Layout_Contains404()
    {
        var html = LayoutProvider.Get404Layout(LayoutProvider.HtmlEncode);
        Assert.Contains("404", html);
    }

    [Fact]
    public void Get404Layout_ContainsReturnHomeLink()
    {
        var html = LayoutProvider.Get404Layout(LayoutProvider.HtmlEncode);
        Assert.Contains("Return home", html);
        Assert.Contains("href=\"/\"", html);
    }

    [Fact]
    public void Get404Layout_DoesNotContainUserContent()
    {
        var html = LayoutProvider.Get404Layout(LayoutProvider.HtmlEncode);
        Assert.DoesNotContain("<p>content</p>", html);
    }

    [Fact]
    public void HtmlEncode_EncodesHtml()
    {
        var result = LayoutProvider.HtmlEncode("<script>alert('xss')</script>");
        Assert.Equal("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;", result);
    }

    [Fact]
    public void HtmlEncode_NullReturnsEmpty()
    {
        Assert.Equal("", LayoutProvider.HtmlEncode(null));
    }

    [Fact]
    public void HtmlEncode_EmptyReturnsEmpty()
    {
        Assert.Equal("", LayoutProvider.HtmlEncode(""));
    }

    [Fact]
    public void ResolveAssetUrl_AbsoluteUrl_ReturnsUnchanged()
    {
        var result = LayoutProvider.ResolveAssetUrl("https://example.com/asset.svg", "/docs");
        Assert.Equal("https://example.com/asset.svg", result);
    }

    [Fact]
    public void ResolveAssetUrl_RootRelativeWithBasePath_PrependsBasePath()
    {
        var result = LayoutProvider.ResolveAssetUrl("/asset.svg", "/docs");
        Assert.Equal("/docs/asset.svg", result);
    }

    [Fact]
    public void ResolveAssetUrl_RootRelativeNoBasePath_ReturnsRootRelative()
    {
        var result = LayoutProvider.ResolveAssetUrl("/asset.svg", "");
        Assert.Equal("/asset.svg", result);
    }

    [Fact]
    public void ResolveAssetUrl_RelativeUrl_ReturnsUnchanged()
    {
        var result = LayoutProvider.ResolveAssetUrl("asset.svg", "/docs");
        Assert.Equal("asset.svg", result);
    }

    [Fact]
    public void ResolveAssetUrl_Null_ReturnsNull()
    {
        var result = LayoutProvider.ResolveAssetUrl(null, "/docs");
        Assert.Null(result);
    }

    [Fact]
    public void ResolveAssetUrl_Empty_ReturnsEmpty()
    {
        var result = LayoutProvider.ResolveAssetUrl("", "/docs");
        Assert.Equal("", result);
    }

    [Fact]
    public void GetLayout_BrandImageRootRelative_WithBasePath()
    {
        var html = LayoutProvider.GetLayout(
            "Title", "<p>content</p>",
            null,
            brandImage: "/brand.svg",
            basePath: "/docs");

        Assert.Contains("<img src=\"/docs/brand.svg\"", html);
    }

    [Fact]
    public void GetLayout_BrandImageRootRelativeNoBasePath_StaysRootRelative()
    {
        var html = LayoutProvider.GetLayout(
            "Title", "<p>content</p>",
            null,
            brandImage: "/brand.svg");

        Assert.Contains("<img src=\"/brand.svg\"", html);
    }

    [Fact]
    public void GetLayout_BrandImageAbsoluteUrl_Unchanged()
    {
        var html = LayoutProvider.GetLayout(
            "Title", "<p>content</p>",
            null,
            brandImage: "https://cdn.example.com/logo.svg",
            basePath: "/docs");

        Assert.Contains("<img src=\"https://cdn.example.com/logo.svg\"", html);
    }

    [Fact]
    public void GetLayout_BrandImageEmoji_RendersMarkSpan()
    {
        var html = LayoutProvider.GetLayout(
            "Title", "<p>content</p>",
            null,
            brandImage: "\U0001F33F",
            basePath: "/docs");

        Assert.Contains("<span class=\"brand-mark\" aria-hidden=\"true\">&#127807;</span>", html);
        Assert.DoesNotContain("<img src=\"/docs/\U0001F33F\"", html);
    }

    [Fact]
    public void GetLayout_BrandImageRelativePath_StillRendersImg()
    {
        var html = LayoutProvider.GetLayout(
            "Title", "<p>content</p>",
            null,
            brandImage: "assets/logo.svg");

        Assert.Contains("<img src=\"assets/logo.svg\"", html);
    }

    [Fact]
    public void GetLayout_NoBrandImage_KeepsDefaultMark()
    {
        var html = LayoutProvider.GetLayout(
            "Title", "<p>content</p>",
            null);

        Assert.Contains("<span class=\"brand-mark\" aria-hidden=\"true\">\U0001F375</span>", html);
    }

    [Fact]
    public void GetLayout_BlankBrandImage_RendersNoMark()
    {
        var html = LayoutProvider.GetLayout(
            "Title", "<p>content</p>",
            null,
            brandImage: "");

        var brandStart = html.IndexOf("class=\"brand\"", StringComparison.Ordinal);
        var brandEnd = html.IndexOf("</a>", brandStart, StringComparison.Ordinal);
        var brandHtml = html[brandStart..brandEnd];

        Assert.DoesNotContain("brand-mark", brandHtml);
        Assert.DoesNotContain("<img", brandHtml);
        Assert.Contains(">Warden</a>", html);
    }

    [Fact]
    public void GetLayout_FaviconRootRelative_WithBasePath()
    {
        var html = LayoutProvider.GetLayout(
            "Title", "<p>content</p>",
            null,
            favicon: "/icon.ico",
            basePath: "/docs");

        Assert.Contains("<link rel=\"icon\" href=\"/docs/icon.ico\"", html);
    }

    [Fact]
    public void GetLayout_FaviconAbsoluteUrl_Unchanged()
    {
        var html = LayoutProvider.GetLayout(
            "Title", "<p>content</p>",
            null,
            favicon: "https://example.com/favicon.ico",
            basePath: "/docs");

        Assert.Contains("<link rel=\"icon\" href=\"https://example.com/favicon.ico\"", html);
    }

    [Fact]
    public void GetLayout_FaviconEmoji_FallbackToDataUri()
    {
        var html = LayoutProvider.GetLayout(
            "Title", "<p>content</p>",
            null,
            favicon: "🔥");

        Assert.Contains("<link rel=\"icon\" href=\"data:image/svg+xml;base64,", html);
        Assert.DoesNotContain("/🔥", html);
    }
}
