using Warden.Services.Theming.Structures;

namespace Warden.Services.Theming;

/// <summary>Built-in page structures. The blog-inherited "editorial" shape was removed, it fit Teatime's blog layout, not a status page; "default" is a new, unrelated status-page-only structure now. To add your own, implement <see cref="IWardenStructure"/> and add a line to <see cref="All"/>.</summary>
public static class StructureRegistry
{
    public static IReadOnlyList<IWardenStructure> All { get; } =
    [
        new CleanStructure(),
        new DefaultStructure(),
        new DashboardStructure()
    ];

    public static IWardenStructure Default { get; } = All[0];

    /// <summary>Unknown names warn and fall back to the default; a typo must never take a site down.</summary>
    public static IWardenStructure Resolve(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Default;

        var trimmed = name.Trim();
        foreach (var structure in All)
        {
            if (string.Equals(structure.Name, trimmed, StringComparison.OrdinalIgnoreCase))
                return structure;
        }

        Serilog.Log.Warning(
            "Unknown structure {Structure}; falling back to {Default}. Available: {Available}",
            trimmed, Default.Name, string.Join(", ", All.Select(s => s.Name)));
        return Default;
    }
}
