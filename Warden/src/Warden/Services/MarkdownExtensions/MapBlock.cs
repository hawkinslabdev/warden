using System.Globalization;
using System.Net;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Warden.Services.MarkdownExtensions;

/// <summary>Renders a ```map fenced block into a self-hosted Leaflet container. Pins carry name/contact/phone/text.</summary>
public static class MapBlock
{
    private sealed record MapSpec
    {
        public int? Zoom { get; init; }
        public string? Center { get; init; }
        public int? Height { get; init; }
        public List<Pin>? Pins { get; init; }
    }

    private sealed record Pin
    {
        public string? Name { get; init; }
        public string? Coords { get; init; }
        public string? Phone { get; init; }
        public string? Contact { get; init; }
        public string? Url { get; init; }
        public string? Text { get; init; }
    }

    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly JsonSerializerOptions Json = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public static string Render(string body)
    {
        MapSpec? spec;
        try { spec = Yaml.Deserialize<MapSpec>(body); }
        catch (YamlDotNet.Core.YamlException) { return Error("Invalid map block."); }
        if (spec is null) return Error("Empty map block.");

        var points = new List<object>();
        foreach (var pin in spec.Pins ?? [])
        {
            if (!TryCoords(pin.Coords, out var lat, out var lng)) continue;
            points.Add(new { lat, lng, name = pin.Name, phone = pin.Phone, contact = pin.Contact, url = pin.Url, text = pin.Text });
        }

        var height = Math.Clamp(spec.Height ?? 400, 160, 900);
        var pinsJson = WebUtility.HtmlEncode(JsonSerializer.Serialize(points, Json));

        var attrs = $" data-pins=\"{pinsJson}\" data-height=\"{height}\"";
        if (spec.Zoom is int z) attrs += $" data-zoom=\"{z}\"";
        if (TryCoords(spec.Center, out var clat, out var clng))
            attrs += $" data-center=\"{clat.ToString(CultureInfo.InvariantCulture)},{clng.ToString(CultureInfo.InvariantCulture)}\"";

        return $"<div class=\"warden-map\" style=\"height:{height}px\"{attrs} role=\"application\" aria-label=\"Map\"></div>";
    }

    private static bool TryCoords(string? raw, out double lat, out double lng)
    {
        lat = lng = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var parts = raw.Split(',', 2);
        return parts.Length == 2
            && double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
            && double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out lng);
    }

    private static string Error(string message) =>
        $"<div class=\"warden-map-error\">{WebUtility.HtmlEncode(message)}</div>";
}
