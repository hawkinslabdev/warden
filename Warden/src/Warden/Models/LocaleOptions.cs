namespace Warden.Models;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>Locale settings for localization: date formatting and the UI string table.</summary>
public sealed class LocaleOptions
{
    /// <summary>BCP 47 culture name (e.g., "nl-NL"). Defaults to invariant format if null.</summary>
    public string? Culture { get; set; }

    /// <summary>Selects the UI string file <c>content/locale/{code}.json</c> (e.g. "nl").
    /// Falls back to <c>lang</c>, then "en". Missing or corrupt file uses the built-in English defaults.</summary>
    public string? Code { get; set; }

    /// <summary>Alias for <see cref="Code"/>, accepted because users write "lang" in the object form.</summary>
    public string? Lang { get; set; }
}

/// <summary>Reads <c>locale</c> as either an object or a bare language code string.</summary>
public sealed class LocaleOptionsConverter : JsonConverter<LocaleOptions>
{
    public override LocaleOptions? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return new LocaleOptions { Code = reader.GetString() };
        return JsonSerializer.Deserialize<LocaleOptions>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, LocaleOptions value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, options);
}
