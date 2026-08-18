using System.Text.Json.Serialization;

namespace Warden.Models;

// id is a short, stable, human-chosen slug, never an autoincrement int
public sealed record MonitorTarget(string Id, string Name, string Url);

// the "monitoring" block in content/config.json; hot-reloaded with the rest of the file
public sealed record MonitoringConfig(
    List<MonitorTarget>? Targets,
    int? IntervalSeconds,
    int? RetentionDays,
    int? IncidentWindowDays,
    int? IncidentMaxShown,
    int? MaintenanceWindowDays,
    int? MaintenanceMaxShown);

public enum MonitorStatus
{
    Unknown = 0,
    Up = 1,
    Down = 2,
    // linked via `monitors:` on an active content/incidents/*.md maintenance window; overrides Up/Down and is excluded from the "some systems down" banner
    Maintenance = 3,
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
