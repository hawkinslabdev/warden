namespace Warden.Configuration;

public sealed record AuthOptions
{
    public string? Issuer { get; init; }

    public string? ClientId { get; init; }

    public string? ClientSecret { get; init; }

    public IReadOnlyList<string> AllowedSubjects { get; init; } = [];

    public string AuthPath { get; init; } = "/auth";

    public string AdminPath { get; init; } = "/admin";

    public string Branch { get; init; } = "main";

    public string? NewFileUrl { get; init; }

    // path of content/incidents inside the repo; set by Program
    public string IncidentPath { get; init; } = "incidents";

    public string? RepoUrl { get; init; }

    public bool GitConfigured { get; init; }

    public bool IsExport { get; init; }

    // http only on loopback; discovery refuses a non-https authority.
    // AllowedSubjects must be non-empty: an unset allowlist previously admitted every subject the
    // issuer would mint a token for, which is fail-open on any shared or self-signup IdP.
    public bool Enabled =>
        !string.IsNullOrEmpty(ClientId)
        && !string.IsNullOrEmpty(ClientSecret)
        && AllowedSubjects.Count > 0
        && Uri.TryCreate(Issuer, UriKind.Absolute, out var uri)
        && (uri.Scheme is "https" || (uri.Scheme is "http" && uri.IsLoopback));

    public bool AdminEnabled => Enabled && !IsExport;

    /// <summary>Cookies may drop the Secure flag only where the issuer is loopback, i.e. local development.</summary>
    public bool IsLoopbackIssuer =>
        Uri.TryCreate(Issuer, UriKind.Absolute, out var loopbackUri) && loopbackUri.IsLoopback;

    public bool RequiresHttpsMetadata =>
        !(Uri.TryCreate(Issuer, UriKind.Absolute, out var uri) && uri.IsLoopback);

    // content/incidents relative to the clone root; outside the clone falls back to "incidents"
    public static string ResolveIncidentPath(string gitRoot, string docsRoot)
    {
        var relative = Path.GetRelativePath(gitRoot, Path.Combine(docsRoot, "incidents"))
            .Replace(Path.DirectorySeparatorChar, '/');
        return relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative)
            ? "incidents"
            : relative;
    }

    // fail closed: no subject, or no allowlist, admits nobody
    public bool Permits(string? subject) =>
        subject is { Length: > 0 } && AllowedSubjects.Contains(subject, StringComparer.Ordinal);

    public static AuthOptions FromEnvironment() => new()
    {
        Issuer = Env("OIDC_ISSUER"),
        ClientId = Env("OIDC_CLIENT_ID"),
        ClientSecret = Env("OIDC_CLIENT_SECRET"),
        AllowedSubjects = (Env("OIDC_ALLOWED_SUBJECTS") ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        AuthPath = "/" + (Env("AUTH_PATH") ?? "auth").Trim('/'),
        AdminPath = "/" + (Env("ADMIN_PATH") ?? "admin").Trim('/'),
        Branch = Env("GIT_BRANCH") ?? "main",
        NewFileUrl = Env("GIT_NEW_FILE_URL"),
        RepoUrl = Env("GIT_URL"),
        GitConfigured = Env("GIT_ENABLED") is "true" or "1",
    };

    // `KEY=` in compose is empty, not absent; the defaults below would misread it
    private static string? Env(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
}
