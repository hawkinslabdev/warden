namespace Warden.Configuration;

// deployment-level setting, bound from appsettings' "Monitoring" section; targets/interval/retention live in content/config.json instead
public sealed record MonitoringOptions
{
    public string DatabasePath { get; init; } = "data/warden.db";
}
