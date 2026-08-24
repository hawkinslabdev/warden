using System.Globalization;
using System.Text.Json;
using Warden.Models;

namespace Warden.Services.Rendering;

/// <summary>Server-side locale table. English defaults are the floor; content/locale/{code}.json
/// overrides them per key. Swapped atomically on content reload. Never served to clients.</summary>
public sealed class Localization
{
    private static readonly JsonSerializerOptions LocaleJsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = false
    };

    private static readonly Dictionary<string, string> Defaults = new(StringComparer.Ordinal)
    {
        // When adding here, make sure to add to the locale/en.json file too, so it can be overridden.
        ["postNavPrevious"] = "Previous",
        ["postNavNext"] = "Next",
        ["pageNavAria"] = "Adjacent pages",
        ["share"] = "Share",
        ["shareTitle"] = "Share this page",
        ["shareCopy"] = "Copy link",
        ["shareCopied"] = "Copied",
        ["lastUpdated"] = "Last updated on",
        ["skipToContent"] = "Skip to content",
        ["themeToggle"] = "Toggle dark mode",
        ["notFoundTitle"] = "Page Not Found",
        ["notFoundMessage"] = "The page you're looking for doesn't exist.",
        ["notFoundHome"] = "Return home",
        ["statusPageTitle"] = "Status",
        ["statusAllOperational"] = "All systems operational",
        ["statusSomeDown"] = "Some systems are experiencing issues",
        ["statusOperational"] = "Operational",
        ["statusDown"] = "Down",
        ["statusUnknown"] = "Unknown",
        ["statusMonitorMaintenance"] = "Maintenance",
        ["statusDegraded"] = "Degraded",
        ["statusUptimeLabel"] = "{0}% uptime ({1})",
        ["statusIncidentsHeading"] = "Incidents",
        ["statusNoIncidents"] = "No incidents.",
        ["statusDownSince"] = "Down since {0}",
        ["statusDegradedSince"] = "Degraded since {0}",
        ["statusOutagePeriod"] = "Down {0} – {1}",
        ["statusResolved"] = "Resolved",
        ["statusIncidentEnded"] = "Ended",
        ["statusIncidentStarted"] = "Started",
        ["statusIncidentBack"] = "￩ Back to status",
        ["statusIncidentAffected"] = "Affected",
        ["statusFilterShowing"] = "Showing {0}",
        ["statusFilterClear"] = "Clear filter",
        ["statusUnavailable"] = "Status data is temporarily unavailable.",
        ["statusNoData"] = "No data",
        ["statusMaintenanceHeading"] = "Maintenance",
        ["statusMaintenancePlanned"] = "Planned",
        ["statusMaintenanceActive"] = "Active",
        ["statusMaintenanceEnded"] = "Completed",
        ["statusMaintenanceEnds"] = "Ends",
        ["statusMaintenanceWindow"] = "{0} – {1}",
        ["statusOngoingIncidentsHeading"] = "Ongoing incidents",
        ["statusOverallUptime"] = "{0}% uptime over the last {1}",
        ["statusResponseTimeChartLabel"] = "Response time, last {0} days",
        ["statusTimezoneAriaLabel"] = "Change displayed timezone",
        ["statusTimezoneSearchPlaceholder"] = "Search timezone…",
    };

    private readonly IReadOnlyDictionary<string, string> _map;

    private Localization(IReadOnlyDictionary<string, string> map, string code)
    {
        _map = map;
        Code = code;
    }

    /// <summary>Active locale code, so content blocks can pick their own per-locale text.</summary>
    public string Code { get; }

    public static Localization Default { get; } = new(Defaults, "en");

    /// <summary>Every built-in string key, so a shipped locale file can be checked for coverage.</summary>
    public static IReadOnlyCollection<string> Keys => Defaults.Keys;

    private static volatile Localization _current = Default;

    public static Localization Current
    {
        get => _current;
        set => _current = value;
    }

    private string this[string key] =>
        _map.TryGetValue(key, out var v) ? v
        : Defaults.TryGetValue(key, out var d) ? d
        : key;

    private string Format(string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, this[key], args);

    public string PostNavPrevious => this["postNavPrevious"];
    public string PostNavNext => this["postNavNext"];
    public string PageNavAria => this["pageNavAria"];
    public string Share => this["share"];
    public string ShareTitle => this["shareTitle"];
    public string ShareCopy => this["shareCopy"];
    public string ShareCopied => this["shareCopied"];
    public string LastUpdated => this["lastUpdated"];
    public string SkipToContent => this["skipToContent"];
    public string ThemeToggle => this["themeToggle"];
    public string NotFoundTitle => this["notFoundTitle"];
    public string NotFoundMessage => this["notFoundMessage"];
    public string NotFoundHome => this["notFoundHome"];
    public string StatusPageTitle => this["statusPageTitle"];
    public string StatusAllOperational => this["statusAllOperational"];
    public string StatusSomeDown => this["statusSomeDown"];
    public string StatusOperational => this["statusOperational"];
    public string StatusDown => this["statusDown"];
    public string StatusUnknown => this["statusUnknown"];
    public string StatusMonitorMaintenance => this["statusMonitorMaintenance"];
    public string StatusDegraded => this["statusDegraded"];
    public string StatusUptimeLabel(double uptimePercent, string windowLabel) => Format("statusUptimeLabel", uptimePercent.ToString("0.##", CultureInfo.InvariantCulture), windowLabel);
    public string StatusIncidentsHeading => this["statusIncidentsHeading"];
    public string StatusNoIncidents => this["statusNoIncidents"];
    public string StatusDownSince(string when) => Format("statusDownSince", when);
    public string StatusDegradedSince(string when) => Format("statusDegradedSince", when);
    public string StatusOutagePeriod(string start, string end) => Format("statusOutagePeriod", start, end);
    public string StatusResolved => this["statusResolved"];
    public string StatusIncidentEnded => this["statusIncidentEnded"];
    public string StatusIncidentStarted => this["statusIncidentStarted"];
    public string StatusIncidentBack => this["statusIncidentBack"];
    public string StatusIncidentAffected => this["statusIncidentAffected"];
    public string StatusFilterShowing(string when) => Format("statusFilterShowing", when);
    public string StatusFilterClear => this["statusFilterClear"];
    public string StatusUnavailable => this["statusUnavailable"];
    public string StatusNoData => this["statusNoData"];
    public string StatusMaintenanceHeading => this["statusMaintenanceHeading"];
    public string StatusMaintenancePlanned => this["statusMaintenancePlanned"];
    public string StatusMaintenanceActive => this["statusMaintenanceActive"];
    public string StatusMaintenanceEnded => this["statusMaintenanceEnded"];
    public string StatusMaintenanceEnds => this["statusMaintenanceEnds"];
    public string StatusMaintenanceWindow(string start, string end) => Format("statusMaintenanceWindow", start, end);
    public string StatusOngoingIncidentsHeading => this["statusOngoingIncidentsHeading"];
    public string StatusOverallUptime(double uptimePercent, string windowLabel) => Format("statusOverallUptime", uptimePercent.ToString("0.##", CultureInfo.InvariantCulture), windowLabel);
    public string StatusResponseTimeChartLabel(int days) => Format("statusResponseTimeChartLabel", days);
    public string StatusTimezoneAriaLabel => this["statusTimezoneAriaLabel"];
    public string StatusTimezoneSearchPlaceholder => this["statusTimezoneSearchPlaceholder"];

    // Overlays content/locale/{code}.json on the defaults. Missing file: silent. Corrupt/unknown keys: warn.
    public static Localization From(string docsPath, Config? config, ILogger logger)
    {
        var code = ResolveCode(config);
        var path = Path.Combine(docsPath, "locale", $"{code}.json");
        if (!File.Exists(path))
            return code == "en" ? Default : new Localization(Defaults, code);

        var filename = Path.GetFileName(path);

        Dictionary<string, string?>? raw;
        try
        {
            var json = File.ReadAllText(path);
            raw = JsonSerializer.Deserialize<Dictionary<string, string?>>(json, LocaleJsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning("Locale file {Filename} is invalid. Falling back to default strings. Reason: {Message}", filename, ex.Message);
            return new Localization(Defaults, code);
        }

        if (raw is null || raw.Count == 0)
            return new Localization(Defaults, code);

        var map = new Dictionary<string, string>(Defaults, StringComparer.Ordinal);
        var deadKeys = new List<string>();
        foreach (var (key, value) in raw)
        {
            if (!Defaults.ContainsKey(key))
            {
                deadKeys.Add(key);
                continue;
            }
            if (!string.IsNullOrEmpty(value))
                map[key] = value;
        }

        if (deadKeys.Count > 0)
            logger.LogWarning("Locale file {Filename} has unknown keys (no such string, ignored): {Keys}",
                filename, string.Join(", ", deadKeys.Order()));

        return new Localization(map, code);
    }

    private static string ResolveCode(Config? config)
    {
        var raw = Config.ResolveLocale(config)?.Code ?? "en";
        return IsValidCode(raw) ? raw.ToLowerInvariant() : "en";
    }

    // Guard the filename: locale codes are short tokens, never paths.
    private static bool IsValidCode(string s)
    {
        if (s.Length is < 2 or > 12) return false;
        foreach (var c in s)
            if (!char.IsAsciiLetterOrDigit(c) && c != '-') return false;
        return true;
    }
}
