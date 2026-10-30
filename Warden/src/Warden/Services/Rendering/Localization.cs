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
        ["serverErrorTitle"] = "This page failed to load",
        ["serverErrorMessage"] = "The server hit an error while building this page. Try again in a minute.",
        ["statusPageTitle"] = "Status",
        ["statusAllOperational"] = "All systems operational",
        ["statusBannerDown"] = "Down: {0}",
        ["statusBannerDegraded"] = "Degraded: {0}",
        ["statusBannerMaintenance"] = "Maintenance: {0}",
        ["statusLastChecked"] = "Last checked {0}",
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
        ["statusNotice"] = "Notice",
        ["statusIncidentAffectedAll"] = "All services",
        ["statusPostedOn"] = "Posted {0}",
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
        ["statusFirstCheck"] = "Waiting for first check",
        ["statusMaintenanceHeading"] = "Maintenance",
        ["statusMaintenancePlanned"] = "Planned",
        ["statusMaintenanceActive"] = "Active",
        ["statusMaintenanceEnded"] = "Completed",
        ["statusMaintenanceEnds"] = "Ends",
        ["statusMaintenanceWindow"] = "{0} – {1}",
        ["statusOngoingIncidentsHeading"] = "Ongoing incidents",
        ["statusOverallUptime"] = "{0}% uptime over the last {1}",
        ["statusOverallCollecting"] = "Collecting uptime data, {0} so far",
        ["statusResponseTimeChartLabel"] = "Response time, last {0} days",
        ["statusTimezoneAriaLabel"] = "Change displayed timezone",
        ["statusTimezoneSearchPlaceholder"] = "Search timezone…",
        ["adminTitle"] = "Admin",
        ["adminSignOut"] = "Sign out",
        ["adminSignedInAs"] = "Signed in as {0}",
        ["adminUnnamedOperator"] = "an operator",
        ["adminMonitorsHeading"] = "Monitors",
        ["adminMonitorsEmptyTitle"] = "No monitors yet",
        ["adminMonitorsEmptyNote"] = "Add an entry to <code>monitoring.targets</code> in <code>config.json</code>. It will then show up here.",
        ["adminStateOperational"] = "Operational",
        ["adminStateDown"] = "Down",
        ["adminStateUnknown"] = "No data",
        ["adminStateOff"] = "Off",
        ["adminEnabled"] = "Enabled",
        ["adminHidden"] = "Hidden",
        ["adminRetries"] = "Retries",
        ["adminResetHistory"] = "Reset history",
        ["adminReorderLabel"] = "Reorder {0}",
        ["adminReorderHint"] = "Drag it, or use the arrow keys",
        ["adminWebhooksHeading"] = "Webhooks",
        ["adminWebhookUrl"] = "URL",
        ["adminWebhookHeaderName"] = "Header name",
        ["adminWebhookHeaderValue"] = "Header value",
        ["adminWebhookSecretKept"] = "Kept unless you replace it",
        ["adminTimingHeading"] = "Timing",
        ["adminInterval"] = "Interval (s)",
        ["adminRetention"] = "Keep for (days)",
        ["adminCooldown"] = "Cooldown (min)",
        ["adminDiscard"] = "Discard",
        ["adminUnsavedChanges"] = "Unsaved changes",
        ["adminSaveChanges"] = "Save changes",
        ["adminIncidentHeading"] = "New incident",
        ["adminIncidentTitle"] = "Title",
        ["adminIncidentStart"] = "Start",
        ["adminIncidentEnd"] = "End",
        ["adminIncidentDescription"] = "Description",
        ["adminIncidentAffected"] = "Affected monitors",
        ["adminIncidentMaintenance"] = "Maintenance",
        ["adminIncidentDegraded"] = "Degraded",
        ["adminIncidentOutage"] = "Outage",
        ["adminIncidentKind"] = "Type",
        ["adminMore"] = "More",
        ["adminMoreFor"] = "More for {0}",
        ["adminIncidentBody"] = "Body",
        ["adminCompose"] = "Create draft",
        ["adminIncidentFileHeading"] = "Incident file",
        ["adminIncidentFileNote"] = "Save this as <code>content/incidents/&lt;slug&gt;.md</code> in your repository.",
        ["adminResetDialogTitle"] = "Reset history?",
        ["adminResetDialogBody"] = "This deletes every saved heartbeat for this monitor. Uptime, the history bar, and the response time chart will start again from empty. Other monitors are not affected.",
        ["adminCancel"] = "Cancel",
        ["adminResetConfirm"] = "Delete history",
        ["adminFlashSaved"] = "Changes saved.",
        ["adminFlashReset"] = "History cleared.",
        ["adminFlashDraftReady"] = "Too long to fill in the form. Copy the file below instead.",
        ["adminFlashWriteFailed"] = "Could not save config.json. Check the file permissions.",
        ["adminFlashUnknownMonitor"] = "There is no monitor with that id.",
        ["adminFlashBadWebhookUrl"] = "A webhook URL must be a full http:// or https:// address.",
        ["adminFlashBadWebhookHeader"] = "A webhook header name or value cannot contain control characters.",
        ["adminFlashBadIncident"] = "Needs a title, a description under 300 characters, and a body under 20,000 characters.",
        ["adminFlashMaintenanceNeedsEnd"] = "A maintenance window needs an end time.",
        ["adminFlashEndBeforeStart"] = "The end time is before the start time.",
        ["adminFlashStaleForm"] = "That form has expired. Reload the page and try again.",
        ["authSignIn"] = "Sign in",
        ["authAdminPanel"] = "Admin panel",
        ["altchaGateTitle"] = "Verifying you're human…",
        ["altchaGateDetail"] = "Solving a quick challenge before showing this page.",
        ["altchaGateErrorTitle"] = "Couldn't verify",
        ["altchaGateErrorDetail"] = "Reload the page to try again.",
        ["altchaGateNoScript"] = "JavaScript is required to continue.",
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
    public string ServerErrorTitle => this["serverErrorTitle"];
    public string ServerErrorMessage => this["serverErrorMessage"];
    public string StatusPageTitle => this["statusPageTitle"];
    public string StatusAllOperational => this["statusAllOperational"];
    public string StatusBannerDown(string names) => Format("statusBannerDown", names);
    public string StatusBannerDegraded(string names) => Format("statusBannerDegraded", names);
    public string StatusBannerMaintenance(string names) => Format("statusBannerMaintenance", names);
    public string StatusLastChecked(string when) => Format("statusLastChecked", when);
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
    public string StatusNotice => this["statusNotice"];
    public string StatusIncidentAffectedAll => this["statusIncidentAffectedAll"];
    public string StatusPostedOn(string when) => Format("statusPostedOn", when);
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
    public string StatusFirstCheck => this["statusFirstCheck"];
    public string StatusMaintenanceHeading => this["statusMaintenanceHeading"];
    public string StatusMaintenancePlanned => this["statusMaintenancePlanned"];
    public string StatusMaintenanceActive => this["statusMaintenanceActive"];
    public string StatusMaintenanceEnded => this["statusMaintenanceEnded"];
    public string StatusMaintenanceEnds => this["statusMaintenanceEnds"];
    public string StatusMaintenanceWindow(string start, string end) => Format("statusMaintenanceWindow", start, end);
    public string StatusOngoingIncidentsHeading => this["statusOngoingIncidentsHeading"];
    public string StatusOverallUptime(double uptimePercent, string windowLabel) => Format("statusOverallUptime", uptimePercent.ToString("0.##", CultureInfo.InvariantCulture), windowLabel);
    public string StatusOverallCollecting(string windowLabel) => Format("statusOverallCollecting", windowLabel);
    public string StatusResponseTimeChartLabel(int days) => Format("statusResponseTimeChartLabel", days);
    public string StatusTimezoneAriaLabel => this["statusTimezoneAriaLabel"];
    public string StatusTimezoneSearchPlaceholder => this["statusTimezoneSearchPlaceholder"];
    public string AdminTitle => this["adminTitle"];
    public string AdminSignOut => this["adminSignOut"];
    public string AdminSignedInAs(string value) => Format("adminSignedInAs", value);
    public string AdminUnnamedOperator => this["adminUnnamedOperator"];
    public string AdminMonitorsHeading => this["adminMonitorsHeading"];
    public string AdminMonitorsEmptyTitle => this["adminMonitorsEmptyTitle"];
    public string AdminMonitorsEmptyNote => this["adminMonitorsEmptyNote"];
    public string AdminStateOperational => this["adminStateOperational"];
    public string AdminStateDown => this["adminStateDown"];
    public string AdminStateUnknown => this["adminStateUnknown"];
    public string AdminStateOff => this["adminStateOff"];
    public string AdminEnabled => this["adminEnabled"];
    public string AdminHidden => this["adminHidden"];
    public string AdminRetries => this["adminRetries"];
    public string AdminResetHistory => this["adminResetHistory"];
    public string AdminReorderLabel(string value) => Format("adminReorderLabel", value);
    public string AdminReorderHint => this["adminReorderHint"];
    public string AdminWebhooksHeading => this["adminWebhooksHeading"];
    public string AdminWebhookUrl => this["adminWebhookUrl"];
    public string AdminWebhookHeaderName => this["adminWebhookHeaderName"];
    public string AdminWebhookHeaderValue => this["adminWebhookHeaderValue"];
    public string AdminWebhookSecretKept => this["adminWebhookSecretKept"];
    public string AdminTimingHeading => this["adminTimingHeading"];
    public string AdminInterval => this["adminInterval"];
    public string AdminRetention => this["adminRetention"];
    public string AdminCooldown => this["adminCooldown"];
    public string AdminDiscard => this["adminDiscard"];
    public string AdminUnsavedChanges => this["adminUnsavedChanges"];
    public string AdminSaveChanges => this["adminSaveChanges"];
    public string AdminIncidentHeading => this["adminIncidentHeading"];
    public string AdminIncidentTitle => this["adminIncidentTitle"];
    public string AdminIncidentStart => this["adminIncidentStart"];
    public string AdminIncidentEnd => this["adminIncidentEnd"];
    public string AdminIncidentDescription => this["adminIncidentDescription"];
    public string AdminIncidentAffected => this["adminIncidentAffected"];
    public string AdminIncidentMaintenance => this["adminIncidentMaintenance"];
    public string AdminIncidentDegraded => this["adminIncidentDegraded"];
    public string AdminIncidentOutage => this["adminIncidentOutage"];
    public string AdminIncidentKind => this["adminIncidentKind"];
    public string AdminMore => this["adminMore"];
    public string AdminMoreFor(string name) => Format("adminMoreFor", name);
    public string AdminIncidentBody => this["adminIncidentBody"];
    public string AdminCompose => this["adminCompose"];
    public string AdminIncidentFileHeading => this["adminIncidentFileHeading"];
    public string AdminIncidentFileNote => this["adminIncidentFileNote"];
    public string AdminResetDialogTitle => this["adminResetDialogTitle"];
    public string AdminResetDialogBody => this["adminResetDialogBody"];
    public string AdminCancel => this["adminCancel"];
    public string AdminResetConfirm => this["adminResetConfirm"];
    public string AdminFlashSaved => this["adminFlashSaved"];
    public string AdminFlashReset => this["adminFlashReset"];
    public string AdminFlashDraftReady => this["adminFlashDraftReady"];
    public string AdminFlashWriteFailed => this["adminFlashWriteFailed"];
    public string AdminFlashUnknownMonitor => this["adminFlashUnknownMonitor"];
    public string AdminFlashBadWebhookUrl => this["adminFlashBadWebhookUrl"];
    public string AdminFlashBadWebhookHeader => this["adminFlashBadWebhookHeader"];
    public string AdminFlashBadIncident => this["adminFlashBadIncident"];
    public string AdminFlashMaintenanceNeedsEnd => this["adminFlashMaintenanceNeedsEnd"];
    public string AdminFlashEndBeforeStart => this["adminFlashEndBeforeStart"];
    public string AdminFlashStaleForm => this["adminFlashStaleForm"];
    public string AuthSignIn => this["authSignIn"];
    public string AuthAdminPanel => this["authAdminPanel"];
    public string AltchaGateTitle => this["altchaGateTitle"];
    public string AltchaGateDetail => this["altchaGateDetail"];
    public string AltchaGateErrorTitle => this["altchaGateErrorTitle"];
    public string AltchaGateErrorDetail => this["altchaGateErrorDetail"];
    public string AltchaGateNoScript => this["altchaGateNoScript"];

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
