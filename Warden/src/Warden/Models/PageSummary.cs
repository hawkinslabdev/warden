namespace Warden.Models;

/// <summary>Public page metadata for /api/pages; deliberately excludes server file paths</summary>
public sealed record PageSummary(string Path, string Title, string? Description, DateTime? LastModified);
