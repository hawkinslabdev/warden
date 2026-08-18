using System.Text.Json;
using Warden.Models;

namespace Warden.Tests;

public sealed class LocaleConfigTests
{
    [Theory]
    [InlineData("""{ "locale": "en" }""", "en", null)]
    [InlineData("""{ "locale": "nl" }""", "nl", null)]
    [InlineData("""{ "locale": { "code": "fr" } }""", "fr", null)]
    [InlineData("""{ "locale": { "lang": "de" } }""", "de", null)]
    [InlineData("""{ "locale": { "culture": "en-GB" } }""", null, "en-GB")]
    [InlineData("""{ "locale": "en", "culture": "en-GB" }""", "en", "en-GB")]
    [InlineData("""{ "locale": { "culture": "en-GB", "lang": "en" } }""", "en", "en-GB")]
    [InlineData("""{ "locale": { "culture": "nl-NL", "code": "nl" } }""", "nl", "nl-NL")]
    [InlineData("""{ "locale": { "culture": "nl-NL" }, "lang": "nl" }""", "nl", "nl-NL")]
    [InlineData("""{ "locale": "en", "culture": "en-GB", "lang": "nl" }""", "en", "en-GB")]
    public void Parses_AllLocaleForms(string json, string? expectedCode, string? expectedCulture)
    {
        var config = JsonSerializer.Deserialize<Config>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        var locale = Config.ResolveLocale(config);

        Assert.Equal(expectedCode, locale?.Code);
        Assert.Equal(expectedCulture, locale?.Culture);
    }

    [Fact]
    public void NoLocaleConfig_ResolvesNull()
    {
        var config = JsonSerializer.Deserialize<Config>("""{ "title": "x" }""");
        Assert.Null(Config.ResolveLocale(config));
    }
}
