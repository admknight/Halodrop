// Morph change: the shelf holds references to files: each once, only ones that exist, the oldest going past the limit;
// taking one off or closing it changes nothing on disk.

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Filee.App.Views;

namespace Filee.App.Tests;

public class ShelfTests
{
    [AvaloniaFact]
    public void Files_are_kept_once_and_closing_leaves_them_on_disk()
    {
        TestServices.EnsureInitialized("en");
        var dir = Directory.CreateTempSubdirectory("morph-shelf-");
        try
        {
            var a = Path.Combine(dir.FullName, "a.txt");
            var b = Path.Combine(dir.FullName, "b photo.png");
            File.WriteAllText(a, "a");
            File.WriteAllBytes(b, [0x89, 0x50, 0x4E, 0x47]); // not a real picture: the file icon stays
            var shelf = new ShelfWindow();
            shelf.Add([a, b, a.ToUpperInvariant(), Path.Combine(dir.FullName, "gone.txt"), dir.FullName]);

            Assert.True(shelf.IsVisible);
            Assert.Equal([a, b, dir.FullName], shelf.Paths);
            Assert.Equal("3", shelf.FindControl<TextBlock>("CountText")!.Text);
            Assert.False(shelf.FindControl<TextBlock>("EmptyText")!.IsVisible);

            shelf.Close();
            Assert.True(File.Exists(a) && File.Exists(b));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
