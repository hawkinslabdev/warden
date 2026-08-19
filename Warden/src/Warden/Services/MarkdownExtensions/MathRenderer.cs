using System.Linq;
using System.Net;
using Jint;
using Jint.Runtime;

namespace Warden.Services.MarkdownExtensions;

public sealed class MathRenderer
{
    // Deep nesting overflows the CLR stack and aborts the process, which is not catchable, so the input is bounded instead.
    // Jint's LimitRecursion is no help: it rejects ordinary formulas and the stack still blows past it.
    // Crash point measured at ~340 levels on a 1 MB stack; real formulas nest under 20. Raise this if one ever trips it.
    private const int MaxNestingDepth = 64;
    private const int MaxLatexLength = 8192;
    private static readonly TimeSpan MaxRenderTime = TimeSpan.FromSeconds(5);

    private readonly Lock _lock = new();
    private Engine? _engine;

    public string RenderToHtml(string latex, bool displayMode)
    {
        if (Reject(latex) is { } reason)
            return MathError(reason);

        lock (_lock)
        {
            try
            {
                var engine = _engine ??= CreateEngine();
                engine.SetValue("__wardenMathInput", latex);
                engine.SetValue("__wardenMathDisplay", displayMode);
                return engine
                    .Evaluate("katex.renderToString(__wardenMathInput, { throwOnError: false, displayMode: __wardenMathDisplay })")
                    .AsString();
            }
            catch (Exception ex) when (ex is JintException or TimeoutException)
            {
                // A tripped constraint leaves the shared engine mid-call; drop it so the next page starts clean.
                _engine = null;
                return MathError(ex.Message);
            }
        }
    }

    /// <summary>Null when the expression is safe to hand to KaTeX, else the reason to render instead.</summary>
    internal static string? Reject(string latex)
    {
        if (latex.Length > MaxLatexLength)
            return $"expression too long ({latex.Length} characters, limit {MaxLatexLength})";

        var depth = 0;
        var deepest = 0;
        for (var i = 0; i < latex.Length; i++)
        {
            if (latex[i] == '\\') { i++; continue; }
            if (latex[i] == '{') deepest = Math.Max(deepest, ++depth);
            else if (latex[i] == '}' && depth > 0) depth--;
        }

        return deepest > MaxNestingDepth
            ? $"expression nested too deeply ({deepest} levels, limit {MaxNestingDepth})"
            : null;
    }

    private static string MathError(string message)
    {
        var trimmed = message.Length > 200 ? message[..200] : message;
        return $"<span class=\"math-error\">{WebUtility.HtmlEncode(trimmed)}</span>";
    }

    private static Engine CreateEngine()
    {
        var engine = new Engine(options => options
            .Strict(false)
            .TimeoutInterval(MaxRenderTime));
        engine.Execute(ReadEmbeddedKaTeX());
        return engine;
    }

    private static string ReadEmbeddedKaTeX()
    {
        var assembly = typeof(MathRenderer).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .First(name => name.EndsWith("katex.min.js", StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded KaTeX resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
