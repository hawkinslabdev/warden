using Markdig;
using Markdig.Extensions.CustomContainers;
using Markdig.Extensions.Mathematics;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Warden.Models;

namespace Warden.Services.MarkdownExtensions;

/// <summary>Replaces Markdig's default renderers for code blocks, custom containers, and math</summary>
public sealed class MarkdownExtension(
    ISyntaxHighlighter syntaxHighlighter,
    CodeGroupIconOptions? codeGroupIcons = null,
    string basePath = "",
    MathRenderer? mathRenderer = null) : IMarkdownExtension
{
    // Math/Custom-containers registered in UseMarkdownExtensions(), not here, to avoid modifying pipeline collection during Setup().
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is not HtmlRenderer htmlRenderer)
            return;

        var codeBlockRenderer = new FencedCodeBlockRenderer(syntaxHighlighter);
        htmlRenderer.ObjectRenderers.ReplaceOrAdd<CodeBlockRenderer>(codeBlockRenderer);
        htmlRenderer.ObjectRenderers.ReplaceOrAdd<HtmlCustomContainerRenderer>(
            new ContainerRenderer(codeBlockRenderer, codeGroupIcons ?? new CodeGroupIconOptions(), basePath));

        if (mathRenderer != null)
        {
            htmlRenderer.ObjectRenderers.ReplaceOrAdd<HtmlMathInlineRenderer>(new MathInlineRenderer(mathRenderer));
            htmlRenderer.ObjectRenderers.ReplaceOrAdd<HtmlMathBlockRenderer>(new MathBlockRenderer(mathRenderer));
        }
    }
}

public static class MarkdownExtensions
{
    /// <summary>Enables custom containers, server-side math rendering, and fenced-code-block notation.</summary>
    public static MarkdownPipelineBuilder UseMarkdownExtensions(
        this MarkdownPipelineBuilder pipeline,
        ISyntaxHighlighter? syntaxHighlighter = null,
        CodeGroupIconOptions? codeGroupIcons = null,
        string basePath = "",
        MathRenderer? mathRenderer = null)
    {
        pipeline.UseCustomContainers();
        pipeline.Extensions.AddIfNotAlready(
            new MarkdownExtension(
                syntaxHighlighter ?? NullSyntaxHighlighter.Instance,
                codeGroupIcons,
                basePath,
                mathRenderer));
        return pipeline;
    }
}
