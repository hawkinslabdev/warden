using YamlDotNet.Serialization;
namespace Warden.Tests;
public sealed class TzProbeTests
{
    [Theory]
    [InlineData("2026-08-18T07:20:00Z")]
    [InlineData("2026-08-18T09:20:00+02:00")]
    [InlineData("2026-08-18 07:20:00")]
    [InlineData("2026-08-18")]
    public void Probe(string raw)
    {
        var d = new DeserializerBuilder().WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance).IgnoreUnmatchedProperties().Build();
        var fm = d.Deserialize<Warden.Models.FrontMatter>($"date: {raw}\n");
        var start = Services.IncidentContent.StartOf(new Models.DocumentationPage("x","x","x",Date: fm.Date));
        throw new Exception($"{raw} -> kind={fm.Date!.Value.Kind} value={fm.Date:O} startOf={start:O} tz={TimeZoneInfo.Local.Id}");
    }
}
