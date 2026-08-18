using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Extensions.Mathematics;

namespace Warden.Services.MarkdownExtensions;

/// <summary>Server-side renders inline <c>$...$</c> math to static KaTeX HTML</summary>
public sealed class MathInlineRenderer(MathRenderer mathRenderer) : HtmlObjectRenderer<MathInline>
{
    protected override void Write(HtmlRenderer renderer, MathInline obj) =>
        renderer.Write(mathRenderer.RenderToHtml(obj.Content.ToString(), displayMode: false));
}
