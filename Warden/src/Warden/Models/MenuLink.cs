namespace Warden.Models;

public sealed record MenuLink
{
    public string? Title { get; init; }
    public string? Path { get; init; }

    /// <summary>Open this link in a new tab. Absolute http(s) and mailto links open externally on their own.</summary>
    public bool External { get; init; }

    /// <summary>Nested items. When present, this entry renders as a hover/focus dropdown instead of a link.</summary>
    public List<MenuLink>? Items { get; init; }
}
