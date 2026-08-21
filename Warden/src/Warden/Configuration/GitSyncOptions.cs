namespace Warden.Configuration;

// deployment-level, bound from appsettings' "Git" section: opt-in `git clone`/`git pull` for the content/
// mount, driven entirely by env vars (Git__Url, Git__Username, Git__Password, Git__Cron) so no manual
// `git clone` or credential-in-URL step is needed before the container starts.
public sealed record GitSyncOptions
{
    public bool Enabled { get; init; }

    public string? Url { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    // standard 5-field cron expression (minute hour day-of-month month day-of-week)
    public string Cron { get; init; } = "*/5 * * * *";
}
