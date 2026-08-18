using System.Linq;
using Jint;

namespace Warden.Services.MarkdownExtensions;

public sealed class MathRenderer
{
    private readonly Lock _lock = new();
    private Engine? _engine;

    public string RenderToHtml(string latex, bool displayMode)
    {
        lock (_lock)
        {
            var engine = _engine ??= CreateEngine();
            engine.SetValue("__wardenMathInput", latex);
            engine.SetValue("__wardenMathDisplay", displayMode);
            return engine
                .Evaluate("katex.renderToString(__wardenMathInput, { throwOnError: false, displayMode: __wardenMathDisplay })")
                .AsString();
        }
    }

    private static Engine CreateEngine()
    {
        var engine = new Engine(options => options.Strict(false));
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
