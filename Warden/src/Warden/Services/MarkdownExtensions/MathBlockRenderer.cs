using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Extensions.Mathematics;

namespace Warden.Services.MarkdownExtensions;

/// <summary>Server-side renders block <c>$$...$$</c> math to static KaTeX HTML</summary>
public sealed class MathBlockRenderer(MathRenderer mathRenderer) : HtmlObjectRenderer<MathBlock>
{
    protected override void Write(HtmlRenderer renderer, MathBlock obj)
    {
        renderer.EnsureLine();
        renderer.WriteLine(mathRenderer.RenderToHtml(obj.Lines.ToString(), displayMode: true));
    }
}
