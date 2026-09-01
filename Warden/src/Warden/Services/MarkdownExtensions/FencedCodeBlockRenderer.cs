using System.Linq;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace Warden.Services.MarkdownExtensions;

/// <summary>Replaces Markdig's default <see cref="FencedCodeBlockRenderer"/> with language wrappers, notation classes, and optional line numbers.</summary>
public sealed class FencedCodeBlockRenderer(ISyntaxHighlighter syntaxHighlighter) : HtmlObjectRenderer<CodeBlock>
{
    protected override void Write(HtmlRenderer renderer, CodeBlock obj)
    {
        if (obj is FencedCodeBlock fenced)
        {
            WriteFenced(renderer, fenced, forceActive: false);
            return;
        }

        WritePlain(renderer, obj);
    }

    public void WriteFenced(HtmlRenderer renderer, FencedCodeBlock obj, bool forceActive, bool isCodeGroupChild = false)
    {
        renderer.EnsureLine();

        if (!renderer.EnableHtmlForBlock)
        {
            renderer.WriteLeafRawLines(obj, true, true);
            return;
        }

        var meta = CodeBlockMeta.Parse(obj.Info, obj.Arguments);
        var rawLines = new string[obj.Lines.Count];
        for (var i = 0; i < obj.Lines.Count; i++)
            rawLines[i] = obj.Lines.Lines[i].Slice.ToString();

        var notated = CodeNotationProcessor.Process(rawLines, meta.HighlightedLines);
        var lang = string.IsNullOrEmpty(meta.Lang) ? "txt" : meta.Lang;

        if (lang is "mermaid" or "nomnoml")
        {
            renderer.Write("<div class=\"").Write(lang).Write("\">");
            renderer.WriteLeafRawLines(obj, true, true);
            renderer.WriteLine("</div>");
            renderer.EnsureLine();
            return;
        }

        if (lang is "map")
        {
            renderer.WriteLine(MapBlock.Render(string.Join('\n', rawLines)));
            renderer.EnsureLine();
            return;
        }

        var showTitleBar = !isCodeGroupChild && !string.IsNullOrEmpty(meta.Title);

        var outerClasses = new List<string>(4) { $"language-{lang}" };
        if (forceActive)
            outerClasses.Add("active");
        if (meta.LineNumbers == true)
            outerClasses.Add("line-numbers-mode");
        if (showTitleBar)
            outerClasses.Add("has-title");
        outerClasses.AddRange(notated.ContainerClasses);

        IReadOnlyList<IReadOnlyList<SyntaxToken>> tokenizedLines;
        try
        {
            tokenizedLines = syntaxHighlighter.TokenizeLines(notated.Lines, lang);
        }
        catch
        {
            // A tokenizer must never break rendering -- fall back to plain escaped text.
            tokenizedLines = notated.Lines
                .Select(line => (IReadOnlyList<SyntaxToken>)[new SyntaxToken(line, null, null)])
                .ToList();
        }

        renderer.Write("<div class=\"").Write(string.Join(' ', outerClasses)).Write("\"");
        if (showTitleBar)
            renderer.Write(" data-filename=\"").WriteEscape(meta.Title!).Write('"');
        // Uses CSS counters on line spans so line numbers can never get out of sync.
        if (meta.LineNumbers == true && meta.LineNumbersStart is { } startAt && startAt != 1)
            renderer.Write(" style=\"--line-start:").Write((startAt - 1).ToString()).Write('"');
        renderer.Write('>');
        if (showTitleBar)
            renderer.Write("<div class=\"code-title\">").WriteEscape(meta.Title!).Write("</div>");
        renderer.Write("<span class=\"lang\">").WriteEscape(lang.Replace('_', ' ')).Write("</span>");

        var theme = syntaxHighlighter.Theme;
        if (theme is { } t)
        {
            renderer.Write("<pre class=\"shiki shiki-themes ").Write(t.LightName).Write(' ').Write(t.DarkName)
                .Write("\" style=\"--shiki-light:").Write(t.LightForeground)
                .Write(";--shiki-dark:").Write(t.DarkForeground)
                .Write(";\" tabindex=\"0\" dir=\"ltr\"><code>");
        }
        else
        {
            renderer.Write("<pre><code>");
        }

        for (var i = 0; i < notated.Lines.Count; i++)
        {
            var classes = notated.LineClasses[i];
            renderer.Write("<span class=\"line");
            foreach (var cls in classes)
                renderer.Write(' ').Write(cls);
            renderer.Write("\">");

            var words = notated.LineWordHighlights[i];
            var spans = meta.WordHighlights.Count == 0 && words.Count == 0
                ? tokenizedLines[i].Select(t => new RenderedSpan(t.Text, t.LightColor, t.DarkColor, false)).ToList()
                : CodeWordHighlighter.Apply(tokenizedLines[i], MergeWords(meta.WordHighlights, words));
            WriteTokens(renderer, spans);

            renderer.Write("</span>");
            if (i < notated.Lines.Count - 1)
                renderer.Write('\n');
        }

        renderer.Write("</code></pre>");

        renderer.WriteLine("</div>");
        renderer.EnsureLine();
    }

    private static HashSet<string> MergeWords(IReadOnlyList<string> blockWide, IReadOnlySet<string> lineSpecific)
    {
        var merged = new HashSet<string>(blockWide);
        merged.UnionWith(lineSpecific);
        return merged;
    }

    private static void WriteTokens(HtmlRenderer renderer, IReadOnlyList<RenderedSpan> spans)
    {
        foreach (var span in spans)
        {
            if (span.IsHighlightedWord)
                renderer.Write("<span class=\"highlighted-word\">");

            if (span.LightColor is null && span.DarkColor is null)
            {
                renderer.WriteEscape(span.Text);
            }
            else
            {
                renderer.Write("<span style=\"--shiki-light:").Write(span.LightColor)
                    .Write(";--shiki-dark:").Write(span.DarkColor).Write("\">");
                renderer.WriteEscape(span.Text);
                renderer.Write("</span>");
            }

            if (span.IsHighlightedWord)
                renderer.Write("</span>");
        }
    }

    private static void WritePlain(HtmlRenderer renderer, CodeBlock obj)
    {
        if (renderer.EnableHtmlForBlock)
            renderer.Write("<pre><code>");

        renderer.WriteLeafRawLines(obj, true, true);

        if (renderer.EnableHtmlForBlock)
            renderer.WriteLine("</code></pre>");

        renderer.EnsureLine();
    }
}
