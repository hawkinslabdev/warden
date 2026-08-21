using System.Text.Json;
using Warden.Models;

namespace Warden.Services;

public static class ThemeJsonLoader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Loads theme.json from the given theme directory; null if missing or malformd, caller falls back to defaults</summary>
    public static ThemeOptions? Load(string themeDir)
    {
        var path = Path.Combine(themeDir, "theme.json");
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ThemeOptions>(json, Options);
        }
        catch (JsonException ex)
        {
            Serilog.Log.Warning(ex, "Failed to parse theme/theme.json, ignoring it");
            return null;
        }
    }
}
