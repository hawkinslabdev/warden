using System.Text.Json;
using Warden.Models;

namespace Warden.Services;

public static class ThemeJsonLoader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Loads wwwroot/theme/theme.json; null if missing or malformd, caller falls back to defaults</summary>
    public static ThemeOptions? Load(string webRootPath)
    {
        var path = Path.Combine(webRootPath, "theme", "theme.json");
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
