// Halodrop change: Morph's folders become Halodrop's on the first start after the rename.
using Filee.Core.Settings;

namespace Filee.Core.Tests;

public sealed class MorphFoldersTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("halodrop-folders-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Morphs_folder_becomes_Halodrops_with_everything_in_it()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Morph", "engines"));
        File.WriteAllText(Path.Combine(_root, "Morph", "settings.json"), "{}");

        MorphFolders.Move(Environment.SpecialFolder.ApplicationData, null, _root);

        Assert.False(Directory.Exists(Path.Combine(_root, "Morph")));
        Assert.True(File.Exists(Path.Combine(_root, "Halodrop", "settings.json")));
        Assert.True(Directory.Exists(Path.Combine(_root, "Halodrop", "engines")));
    }

    [Fact]
    public void An_existing_Halodrop_folder_is_never_replaced()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Morph"));
        File.WriteAllText(Path.Combine(_root, "Morph", "settings.json"), "old");
        Directory.CreateDirectory(Path.Combine(_root, "Halodrop"));
        File.WriteAllText(Path.Combine(_root, "Halodrop", "settings.json"), "new");

        MorphFolders.Move(Environment.SpecialFolder.ApplicationData, null, _root);

        Assert.Equal("new", File.ReadAllText(Path.Combine(_root, "Halodrop", "settings.json")));
        Assert.True(Directory.Exists(Path.Combine(_root, "Morph")));
    }

    [Fact]
    public void Nothing_happens_without_a_Morph_folder()
    {
        MorphFolders.Move(Environment.SpecialFolder.ApplicationData, null, _root);
        Assert.Empty(Directory.GetFileSystemEntries(_root));
    }
}

// Halodrop change: a face past Filee's 12 slices grows the donut a little per slice.
public sealed class DonutGrowthTests
{
    [Theory]
    [InlineData(180, 8, 180)]
    [InlineData(180, 12, 180)]
    [InlineData(180, 14, 201.6)]
    [InlineData(180, 16, 223.2)]
    [InlineData(260, 16, 320)]
    [InlineData(330, 16, 330)]
    public void Only_a_face_past_twelve_slices_grows_the_donut(double radius, int slices, double expected) =>
        Assert.Equal(expected, Filee.Core.Settings.DonutSettings.GrowForSlices(radius, slices), 3);
}
