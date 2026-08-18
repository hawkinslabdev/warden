namespace Warden.Models;

public readonly record struct PagePath
{
    public string Value { get; }

    public PagePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Path cannot be empty", nameof(value));
        Value = value.Trim('/').ToLowerInvariant();
    }

    public static PagePath FromFile(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/');
        if (Path.GetFileName(normalized).Equals("index.md", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(normalized)?.Trim('/') ?? "";
            return dir.Length > 0 ? new PagePath(dir) : new PagePath("index");
        }
        var noExt = Path.ChangeExtension(normalized, null) ?? normalized;
        return new PagePath(noExt.Trim('/'));
    }

    public static string SlugifySegment(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }

    public string[] Segments => Value.Split('/', StringSplitOptions.RemoveEmptyEntries);
    public override string ToString() => Value;
    public static implicit operator string(PagePath path) => path.Value;
}
