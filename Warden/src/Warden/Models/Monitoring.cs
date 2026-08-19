using System.Text.Json.Serialization;

namespace Warden.Models;

// id is a short, stable, human-chosen slug, never an autoincrement int
// Type defaults to "http" so existing configs (no "type" field) keep working unchanged
public sealed record MonitorTarget(
    string Id,
    string Name,
    string? Url = null,
    string Type = "http",
    string? Host = null,
    int? Port = null,
    bool? Secure = null,
    string? DbType = null,
    string? ExpectedIp = null,
    int? WarnDaysBefore = null,
    string? ExpectedJsonPath = null,
    string? ExpectedValue = null,
    int? ExpectedStatus = null,
    // consecutive failures required before a check is recorded as down; unset/0 keeps the old behavior (down on the first failure)
    int? Retries = null,
    // skips TLS certificate validation for http/service_backend checks - for a self-signed internal service
    bool? Insecure = null,
    // dns type only: query this resolver directly instead of the OS default
    string? DnsServer = null,
    // dns type only: "ipv4" or "ipv6" to query only that record type; unset queries both
    string? Family = null,
    // dashboard structure, monitoring.group: "custom" only: this target's own group heading; falls back to its type's label when unset
    string? Group = null);

// the "monitoring" block in content/config.json; hot-reloaded with the rest of the file
public sealed record MonitoringConfig(
    List<MonitorTarget>? Targets,
    int? IntervalSeconds,
    int? RetentionDays,
    int? IncidentWindowDays,
    int? IncidentMaxShown,
    int? MaintenanceWindowDays,
    int? MaintenanceMaxShown,
    // opt-in only; unset renders one flat grid. "type" groups by each target's type, "custom" groups by each target's own "group" field (falling back to its type)
    string? Group = null,
    // opt-in only; unset keeps a content/incidents/*.md file's own folder placement as its URL. "year" -> /incidents/{year}/{slug}/, "year-month" -> /incidents/{year}/{month}/{slug}/, derived from the incident's date front matter regardless of which folder the file actually lives in
    string? IncidentUrlPattern = null);

public enum MonitorStatus
{
    Unknown = 0,
    Up = 1,
    Down = 2,
    // linked via `monitors:` on an active content/incidents/*.md maintenance window; overrides Up/Down and is excluded from the "some systems down" banner
    Maintenance = 3,
    // an active incident declaring `status: degraded`: reachable but impaired, so the probe keeps passing and uptime stays honest while the badge says otherwise
    Degraded = 4,
}

// the json payload stored in a heartbeat row's "data" column
public sealed record HeartbeatPayload(
    bool Up,
    [property: JsonPropertyName("responseMs")] int? ResponseMs,
    string? Error);

public sealed record HeartbeatRecord(string Id, string MonitorId, DateTimeOffset Timestamp, HeartbeatPayload Data);

// /api/status response shapes - "up"/"down"/"unknown"/"active"/"planned" are a stable machine contract, not localized UI text
public sealed record ApiMonitorStatus(string Id, string Name, string Status, double? UptimePercent24h, DateTimeOffset? LastCheckedAt);
public sealed record ApiIncident(string Slug, string Title, string? Description, DateTimeOffset Start, DateTimeOffset? End, string Status);
public sealed record ApiMaintenanceWindow(string Slug, string Title, DateTimeOffset Start, DateTimeOffset End, string? Description, string Status);
public sealed record StatusApiResponse(List<ApiMonitorStatus> Monitors, List<ApiIncident> Incidents, List<ApiMaintenanceWindow> Maintenance);

// one calendar day's aggregate for the history bar
public sealed record DailyStatus(DateOnly Day, MonitorStatus Status);

// one calendar day's average response time for the latency chart; null when no heartbeat landed that day
public sealed record DailyResponseTime(DateOnly Day, double? AvgResponseMs);
