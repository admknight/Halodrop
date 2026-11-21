// Morph change: Filee's download mirror (dl.filee.sh) hosts Filee's releases, never Morph's, so Morph never asks it for
// anything. Replaces Filee's UpdateServiceMirrorTests, which test the mirror Morph turned off.

using Filee.App.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Filee.App.Tests;

public class MorphUpdateMirrorTests
{
    [Theory]
    [InlineData("win", "Morph-2.5.0-win-Setup.exe")]
    [InlineData("win-arm64", "Morph-2.5.0-win-arm64-Setup.exe")]
    [InlineData("win", "Morph-2.5.0-SHA256SUMS.txt.sig")]
    [InlineData("linux-x64", "Morph-2.5.0-linux-x64.tar.gz")]
    public void No_file_ever_comes_from_Filees_mirror(string target, string name)
    {
        Assert.False(UpdateService.UseFileeMirror);
        var updates = new UpdateService(NullLogger<UpdateService>.Instance) { TargetPlatform = target };
        Assert.Null(updates.MirrorUrl(name));
    }

    [Fact]
    public void Releases_come_from_Morphs_repository_only()
    {
        Assert.DoesNotContain("KnifeLemon/Filee", UpdateService.RepositoryUrl, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("filee.sh", UpdateService.RepositoryUrl, StringComparison.OrdinalIgnoreCase);
    }
}
