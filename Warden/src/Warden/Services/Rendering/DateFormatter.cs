using System.Globalization;
using System.Text.RegularExpressions;
using Warden.Models;

namespace Warden.Services.Rendering;

/// <summary>Centralizes locale-aware date rendering. Human dates follow the configured culture
/// (native month order and names); machine dates (<c>datetime</c> attrs, sitemap) stay ISO/invariant.
/// The active instance is swapped atomically when <c>config.json</c> reloads.</summary>
public sealed partial class DateFormatter
{
    // en-GB is the built-in default when no locale is configured (day-first, English months).
    private static readonly CultureInfo DefaultCulture = CultureInfo.GetCultureInfo("en-GB");
    // Guard fallbacks (used only if a culture's derived pattern is unusable); en-GB order.
    private const string DefaultMedium = "d MMM yyyy";
    private const string DefaultMonthDay = "d MMM";

    private static volatile DateFormatter _current = From(null);

    /// <summary>Process-wide formatter. Read on every render; replaced on config reload.</summary>
    public static DateFormatter Current
    {
        get => _current;
        set => _current = value;
    }

    private readonly CultureInfo _culture;
    private readonly string _mediumPattern;
    private readonly string _monthDayPattern;

    private DateFormatter(CultureInfo culture, string mediumPattern, string monthDayPattern)
    {
        _culture = culture;
        _mediumPattern = mediumPattern;
        _monthDayPattern = monthDayPattern;
    }

    /// <summary>Builds a formatter from locale config. Unset or unknown culture falls back to en-GB.</summary>
    public static DateFormatter From(LocaleOptions? locale)
    {
        var culture = ResolveCulture(locale?.Culture);
        var medium = MediumPattern(culture);
        return new DateFormatter(culture, medium, MonthDayPattern(medium));
    }

    /// <summary>Abbreviated date with year, e.g. "Jul 16, 2026" (en-US) or "16 jul 2026" (nl-NL).</summary>
    public string Medium(DateTime date) => date.ToString(_mediumPattern, _culture);

    /// <summary>Abbreviated date without year, e.g. "Jul 16" or "16 jul".</summary>
    public string MonthDay(DateTime date) => date.ToString(_monthDayPattern, _culture);

    /// <summary>Machine-readable ISO date for <c>datetime</c> attributes; always invariant.</summary>
    public static string Iso(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static CultureInfo ResolveCulture(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return DefaultCulture;
        try { return CultureInfo.GetCultureInfo(name); }
        catch (CultureNotFoundException) { return DefaultCulture; }
    }

    // Drops the weekday and abbreviates the month, keeping the locale's native field order.
    private static string MediumPattern(CultureInfo culture)
    {
        var pattern = WeekdayRegex().Replace(culture.DateTimeFormat.LongDatePattern, string.Empty);
        pattern = Tidy(pattern.Replace("MMMM", "MMM"));
        return pattern.Contains('M') && pattern.Contains('d') ? pattern : DefaultMedium;
    }

    private static string MonthDayPattern(string mediumPattern)
    {
        var pattern = Tidy(YearRegex().Replace(mediumPattern, string.Empty));
        return pattern.Contains('M') && pattern.Contains('d') ? pattern : DefaultMonthDay;
    }

    private static string Tidy(string pattern) =>
        SpaceRegex().Replace(pattern, " ").Trim().Trim(',').Trim();

    [GeneratedRegex(@"[,\s]*dddd[,\s]*")]
    private static partial Regex WeekdayRegex();

    [GeneratedRegex(@"[,\s]*\byyyy\b")]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex SpaceRegex();
}
