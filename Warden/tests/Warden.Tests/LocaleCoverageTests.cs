using System.Text.Json;
using Warden.Services.Rendering;

namespace Warden.Tests;

/// <summary>Guards against a new string key being added to the table but not to the shipped translations.</summary>
public sealed class LocaleCoverageTests
{
    private static readonly string LocaleDir = Path.Combine(AppContext.BaseDirectory, "content", "locale");

    public static TheoryData<string> LocaleFiles()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(LocaleDir, "*.json"))
            data.Add(Path.GetFileName(path));
        return data;
    }

    [Fact]
    public void EnglishLocaleFileShips()
    {
        var codes = Directory.GetFiles(LocaleDir, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f)!)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("en", codes);
    }

    [Theory]
    [MemberData(nameof(LocaleFiles))]
    public void LocaleCoversEveryStringKey(string file)
    {
        var json = File.ReadAllText(Path.Combine(LocaleDir, file));
        var translated = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;

        var missing = Localization.Keys.Where(k => !translated.ContainsKey(k)).Order().ToArray();
        Assert.True(missing.Length == 0, $"{file} is missing: {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(LocaleFiles))]
    public void LocaleCarriesNoUnknownKey(string file)
    {
        var json = File.ReadAllText(Path.Combine(LocaleDir, file));
        var translated = JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;

        var known = Localization.Keys.ToHashSet(StringComparer.Ordinal);
        var stray = translated.Keys.Where(k => !known.Contains(k)).Order().ToArray();
        Assert.True(stray.Length == 0, $"{file} has keys no longer in the table: {string.Join(", ", stray)}");
    }
}
