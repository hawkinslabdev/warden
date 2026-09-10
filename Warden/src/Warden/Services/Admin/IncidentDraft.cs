using System.Globalization;
using System.Text;
using Warden.Configuration;

namespace Warden.Services.Admin;

public sealed record IncidentDraft(
    string Title,
    DateTimeOffset Start,
    DateTimeOffset? End,
    bool Maintenance,
    IReadOnlyList<string> Monitors,
    bool Degraded,
    string? Description,
    string Body)
{
    // above this the panel shows the file to copy instead of a prefilled URL
    public const int MaxPrefillLength = 6000;

    public string Markdown()
    {
        var sb = new StringBuilder();
        sb.Append("---\n");
        sb.Append("title: ").Append(YamlScalar(Title)).Append('\n');
        sb.Append("date: ").Append(Iso(Start)).Append('\n');
        if (End is { } end)
            sb.Append("end: ").Append(Iso(end)).Append('\n');
        if (Maintenance)
            sb.Append("maintenance: true\n");
        if (Monitors.Count > 0)
            sb.Append("monitors: [").Append(string.Join(", ", Monitors)).Append("]\n");
        if (Degraded && !Maintenance)
            sb.Append("status: degraded\n");
        if (!string.IsNullOrWhiteSpace(Description))
            sb.Append("description: ").Append(YamlScalar(Description)).Append('\n');
        sb.Append("---\n\n");
        sb.Append(Body.Replace("\r\n", "\n").TrimEnd()).Append('\n');
        return sb.ToString();
    }

    public string Slug() => Slugify(Title) is { Length: > 0 } s ? s : "incident";

    private static string Iso(DateTimeOffset when) =>
        when.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

    // quoted; a bare scalar with ": " or a leading "-" reparses as structure
    internal static string YamlScalar(string? value) =>
        "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "") + "\"";

    internal static string Slugify(string title)
    {
        var sb = new StringBuilder(title.Length);
        foreach (var ch in title.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch))
                sb.Append(ch);
            else if ((ch is ' ' or '-' or '_' or '.' or '/') && sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        return slug.Length <= 60 ? slug : slug[..60].TrimEnd('-');
    }

    // null when no remote is configured or the file exceeds MaxPrefillLength
    public string? NewFileUrl(AuthOptions auth)
    {
        var markdown = Markdown();
        var baseUrl = ResolveBase(auth);
        if (baseUrl is null)
            return null;

        var url = $"{baseUrl}?filename={Uri.EscapeDataString(Slug())}.md&value={Uri.EscapeDataString(markdown)}";
        return url.Length > MaxPrefillLength ? null : url;
    }

    internal static string? ResolveBase(AuthOptions auth)
    {
        if (auth.NewFileUrl is { Length: > 0 } explicitUrl)
            return explicitUrl.TrimEnd('/');
        if (!auth.GitConfigured || auth.RepoUrl is not { Length: > 0 } repo)
            return null;
        if (!Uri.TryCreate(repo, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return null;

        var web = $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";
        if (web.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            web = web[..^4];
        web = web.TrimEnd('/');

        var segment = uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ? "new" : "_new";
        return $"{web}/{segment}/{Uri.EscapeDataString(auth.Branch)}/{auth.IncidentPath}";
    }
}
