// Morph change: screenshots of Morph's own windows for the README (docs/media): the shelf with files on it, and the
// card the colour palette leaves. They also check the windows lay out (nothing empty, the swatches there).

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Filee.App.Services;
using Filee.App.Views;
using ImageMagick;

namespace Filee.App.Tests;

public class MorphFeatureScreenshotTests
{
    [AvaloniaFact]
    public void The_shelf_with_pictures_and_a_document()
    {
        TestServices.EnsureInitialized("en");
        var dir = Directory.CreateTempSubdirectory("morph-shelf-shot-");
        try
        {
            string Picture(string name, string from, string to)
            {
                var path = Path.Combine(dir.FullName, name);
                using var image = new MagickImage($"gradient:{from}-{to}", 320, 220);
                image.Write(path);
                return path;
            }
            var files = new[]
            {
                Picture("site visit 01.jpg", "#7C5CFF", "#22D3EE"),
                Picture("site visit 02.jpg", "#F59E0B", "#EF4444"),
                Picture("roof detail.png", "#10B981", "#1E3A8A"),
                Path.Combine(dir.FullName, "Quotation rev B.pdf"),
            };
            File.WriteAllText(files[3], "%PDF-1.4");
            var shelf = new ShelfWindow();
            AppHost.Get<ThemeService>().ApplyWindowClass(shelf);
            shelf.Add(files);
            RenderTests.Pump();
            Thread.Sleep(400); // the thumbnails load in the background
            RenderTests.Pump();
            Assert.Equal(4, shelf.Paths.Count);
            RenderTests.Save(shelf, "morph-shelf-en.png");
            shelf.Close();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [AvaloniaFact]
    public void The_colour_palette_card()
    {
        TestServices.EnsureInitialized("en");
        var card = new QuickNoticeWindow("Colours copied", null, problem: false);
        AppHost.Get<ThemeService>().ApplyWindowClass(card);
        card.ShowSwatches(["#2E6B4F", "#E8C07A", "#7C5CFF", "#22D3EE", "#F2F2EE", "#1E2433"]);
        card.Show();
        RenderTests.Pump();
        Assert.True(card.FindControl<WrapPanel>("Swatches")!.IsVisible);
        RenderTests.Save(card, "morph-palette-en.png");
        card.Close();
    }
}
