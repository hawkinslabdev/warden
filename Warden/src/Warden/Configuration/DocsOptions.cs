namespace Warden.Configuration;

public sealed record DocsOptions
{
    public string RootPath { get; init; } = "content";
    public string? DefaultPage { get; init; } = "index";
    public bool EnableHotReload { get; init; } = true;
    public string? BasePath { get; init; }

    // public origin for canonical URLs, feeds and robots.txt; unset builds them from the caller-supplied Host header
    public string? PublicBaseUrl { get; init; }
}
