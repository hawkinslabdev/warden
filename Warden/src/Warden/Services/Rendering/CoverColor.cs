using System.Globalization;
using System.Text.RegularExpressions;

namespace Warden.Services.Rendering;

/// <summary>A <c>cover:</c> given as a hex code paints a flatt block instead of loading a image</summary>
public static partial class CoverColor
{
    private const string OnLight = "#14171a";
    private const string OnDark = "#f5f3ee";

    /// <summary>Only a literal hex code renders as colour; anything else will be treated as an URL. Nothing except a match here ever reaches a style attribute</summary>
    public static bool TryParse(string? value, out string hex)
    {
        hex = string.Empty;
        if (value is null) return false;

        var trimmed = value.Trim();
        if (!HexRegex().IsMatch(trimmed)) return false;

        hex = trimmed.ToLowerInvariant();
        return true;
    }

    /// <summary>Ink that stays legible on <paramref name="hex"/> so a monogram never sinks into its cover</summary>
    public static string InkFor(string hex)
    {
        var rgb = Expand(hex);
        if (rgb.Length < 6) return OnDark;

        var r = Channel(rgb, 0);
        var g = Channel(rgb, 2);
        var b = Channel(rgb, 4);

        return (299 * r + 587 * g + 114 * b) / 1000 > 140 ? OnLight : OnDark;
    }

    private static string Expand(string hex)
    {
        var body = hex.TrimStart('#');
        if (body.Length is 3 or 4)
            return string.Concat(body[..3].Select(c => new string(c, 2)));
        return body.Length >= 6 ? body[..6] : string.Empty;
    }

    private static int Channel(string rgb, int offset) =>
        int.Parse(rgb.AsSpan(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^#(?:[0-9a-fA-F]{3,4}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")]
    private static partial Regex HexRegex();
}
