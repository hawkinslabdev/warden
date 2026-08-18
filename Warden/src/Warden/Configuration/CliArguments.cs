namespace Warden.Configuration;

public sealed record CliArguments(string? ExportDir, string? ExportBaseUrl, string? BasePath, string? Theme, string? Structure)
{
    public static CliArguments Parse(string[] args)
    {
        string? exportDir = null;
        string? exportBaseUrl = null;
        string? basePath = null;
        string? theme = null;
        string? structure = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--export" when i + 1 < args.Length: exportDir = args[++i]; break;
                case "--base-url" when i + 1 < args.Length: exportBaseUrl = args[++i]; break;
                case "--base-path" when i + 1 < args.Length: basePath = args[++i]; break;
                case "--theme" when i + 1 < args.Length: theme = args[++i]; break;
                case "--structure" when i + 1 < args.Length: structure = args[++i]; break;
            }
        }

        return new CliArguments(exportDir, exportBaseUrl, basePath, theme, structure);
    }
}
