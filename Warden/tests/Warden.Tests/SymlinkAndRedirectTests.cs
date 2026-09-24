using Microsoft.AspNetCore.Http;
using Warden.Services;

namespace Warden.Tests;

/// <summary>Contributor-writable content must not reach server files through symlinks or redirect off-origin</summary>
public sealed class SymlinkAndRedirectTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("links-").FullName;
    private readonly string _outside = Directory.CreateTempSubdirectory("outside-").FullName;

    [Fact]
    public void SymlinkedFilesAndDirectories_AreHiddenFromEnumerationAndServing()
    {
        var secret = Path.Combine(_outside, "secret.md");
        File.WriteAllText(secret, "dummy-secret");
        File.WriteAllText(Path.Combine(_root, "real.md"), "# Real");
        File.CreateSymbolicLink(Path.Combine(_root, "leak.md"), secret);
        Directory.CreateSymbolicLink(Path.Combine(_root, "linkdir"), _outside);

        var files = Directory.GetFiles(_root, "*.md", ContentLinks.NoLinks).Select(Path.GetFileName);
        Assert.Equal(["real.md"], files);

        Assert.True(ContentLinks.HasLink(_root, Path.Combine(_root, "leak.md")));
        Assert.True(ContentLinks.HasLink(_root, Path.Combine(_root, "linkdir", "secret.md")));
        Assert.False(ContentLinks.HasLink(_root, Path.Combine(_root, "real.md")));

        var provider = new ContentLinks.NoLinkFileProvider(_root);
        Assert.False(provider.GetFileInfo("/leak.md").Exists);
        Assert.False(provider.GetFileInfo("/linkdir/secret.md").Exists);
        Assert.True(provider.GetFileInfo("/real.md").Exists);
    }

    [Theory]
    [InlineData("/\\evil.example", false)]
    [InlineData("\\evil.example", false)]
    [InlineData("/\t/evil.example", false)]
    [InlineData("/guide/", true)]
    public void RelativeRedirect_ThatCouldLeaveOrigin_IsRefused(string target, bool allowed)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("docs.example");
        Assert.Equal(allowed, PageRequestHandler.TryResolveRedirect(target, "", context, null, out _));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch (IOException) { }
        try { Directory.Delete(_outside, true); } catch (IOException) { }
    }
}
