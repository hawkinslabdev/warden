namespace Warden.Models;

/// <summary>What /health reports to an uptime probe.</summary>
public sealed record HealthResponse(string Status, long BuildVersion, int Pages, long UptimeSeconds);
