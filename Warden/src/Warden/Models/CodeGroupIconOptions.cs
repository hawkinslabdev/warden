namespace Warden.Models;

/// <summary>Bound from `Docs:CodeGroupIcons` (appsettings.json), not warden.json -- pipeline is built once at startup.</summary>
public sealed record CodeGroupIconOptions
{
    public bool Enabled { get; init; } = true;

    public string BaseUrl { get; init; } = "/icons";

    public string Format { get; init; } = "svg";

    public Dictionary<string, string>? Overrides { get; init; }

    /// <summary>Icon slugs that actually ship under <c>BaseUrl</c>, scanned at startup. When set,
    /// a tab title auto-slugified to a missing icon renders no icon instead of a 404. Explicit
    /// <c>icon:</c> slugs and <c>Overrides</c> always render.</summary>
    public IReadOnlySet<string>? Available { get; init; }
}
