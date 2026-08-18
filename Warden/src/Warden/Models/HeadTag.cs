namespace Warden.Models;

public record HeadTag
{
    public string Tag { get; init; } = string.Empty;
    public Dictionary<string, string>? Attrs { get; init; }
    public string? Content { get; init; }
}
