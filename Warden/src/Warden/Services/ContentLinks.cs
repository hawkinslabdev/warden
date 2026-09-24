using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Warden.Services;

/// <summary>
/// Keeps symlinks in contributor-writable trees from being followed into served content.
/// </summary>
/// <remarks>
/// A contributor can commit a symlink (git pull restores it as-is) pointing at /proc/self/environ or any file the process can read; the check has to happen where content is read, not only at clone time.
/// </remarks>
public static class ContentLinks
{
    /// <summary>Recursive enumeration that neither returns nor descends into symlinks.</summary>
    public static readonly EnumerationOptions NoLinks = new()
    {
        RecurseSubdirectories = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
        MatchType = MatchType.Win32,
    };

    /// <summary>True when <paramref name="path"/> or any directory between it and <paramref name="root"/> is a link.</summary>
    public static bool HasLink(string root, string path)
    {
        var stop = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        for (var p = Path.GetFullPath(path); p.Length > stop.Length; p = Path.GetDirectoryName(p)!)
        {
            if (new FileInfo(p).LinkTarget is not null)
                return true;
        }
        return false;
    }

    /// <summary>Wraps a <see cref="PhysicalFileProvider"/> so a path through a symlink reads as missing.</summary>
    public sealed class NoLinkFileProvider(string root) : IFileProvider
    {
        private readonly PhysicalFileProvider _inner = new(root);

        public IFileInfo GetFileInfo(string subpath)
        {
            var file = _inner.GetFileInfo(subpath);
            return file.PhysicalPath is { } physical && HasLink(root, physical)
                ? new NotFoundFileInfo(subpath)
                : file;
        }

        public IDirectoryContents GetDirectoryContents(string subpath) => _inner.GetDirectoryContents(subpath);

        public IChangeToken Watch(string filter) => _inner.Watch(filter);
    }
}
