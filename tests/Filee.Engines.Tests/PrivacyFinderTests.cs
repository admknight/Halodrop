// Morph change: the face and text finders behind the Hide window's "Find faces" and "Find text": real photos with faces,
// a picture with printed text, and an empty picture that has neither.

using Filee.Engines.Ocr;
using Filee.Engines.Privacy;
using ImageMagick;

namespace Filee.Engines.Tests;

public class PrivacyFinderTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private static string Sample(string name) => Path.Combine(AppContext.BaseDirectory, "Privacy", name);

    [Theory]
    [InlineData("faces-1.jpg")]
    [InlineData("faces-3.jpg")]
    public void Faces_in_photos_are_found_inside_the_picture(string name)
    {
        Assert.True(FaceFinder.IsAvailable, FaceFinder.ModelPath);
        using var photo = new MagickImage(Sample(name));
        var faces = FaceFinder.Find(photo);
        TestContext.Current.SendDiagnosticMessage($"{name}: {faces.Count} face(s) in {photo.Width}x{photo.Height}");
        Assert.NotEmpty(faces);
        Assert.All(faces, f =>
        {
            Assert.InRange(f.X, 0, 1);
            Assert.InRange(f.Right, 0, 1.0001);
            Assert.InRange(f.Bottom, 0, 1.0001);
            Assert.True(f.Width is > 0.005 and < 0.9 && f.Height is > 0.005 and < 0.9, f.ToString());
        });
        // Each face once: no two finds mostly on top of each other.
        for (var i = 0; i < faces.Count; i++)
            for (var j = i + 1; j < faces.Count; j++)
                Assert.True(faces[i].Overlap(faces[j]) < 0.5, $"{faces[i]} / {faces[j]}");
    }

    [Fact]
    public void An_empty_picture_has_no_faces()
    {
        using var empty = new MagickImage(new MagickColor("#88AACC"), 800, 600);
        Assert.Empty(FaceFinder.Find(empty));
    }

    [Fact]
    public void The_surest_of_overlapping_finds_is_kept()
    {
        var kept = FaceFinder.KeepBest([(new FoundArea(0.1, 0.1, 0.2, 0.2), 0.8f), (new FoundArea(0.11, 0.1, 0.2, 0.2), 0.95f),
            (new FoundArea(0.6, 0.6, 0.1, 0.1), 0.9f)], 0.3);
        Assert.Equal([new FoundArea(0.11, 0.1, 0.2, 0.2), new FoundArea(0.6, 0.6, 0.1, 0.1)], kept);
    }

    [Fact]
    public void Tesseract_lines_become_areas_and_doubtful_words_dont_count()
    {
        const string tsv = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext\n" +
            "5\t1\t1\t1\t1\t1\t100\t50\t80\t20\t91.5\tJohn\n" +
            "5\t1\t1\t1\t1\t2\t190\t52\t90\t20\t88\tSmith\n" +
            "5\t1\t1\t1\t2\t1\t100\t200\t50\t20\t12\t~~\n" +
            "4\t1\t1\t1\t1\t0\t100\t50\t180\t22\t-1\t\n";
        var line = Assert.Single(TextFinder.Lines(tsv, 1000, 500));
        Assert.True(line.X < 0.1 && line.Right > 0.28 && line.Y < 0.1 && line.Bottom > 0.144, line.ToString());
    }

    [Fact]
    public async Task Printed_text_is_found_and_an_empty_picture_has_none()
    {
        Assert.SkipUnless(TextFinder.IsAvailable, "Tesseract isn't installed");
        var eng = new[] { "/usr/share/tesseract-ocr/5/tessdata/eng.traineddata", "/usr/share/tesseract-ocr/4.00/tessdata/eng.traineddata" }
            .FirstOrDefault(File.Exists);
        Func<string, CancellationToken, Task<string>>? data = eng is null ? null : (_, _) => Task.FromResult(eng);
        Assert.SkipWhen(data is null && !OcrLanguages.IsInstalled("eng"), "No English data for Tesseract here");

        using var card = new MagickImage(MagickColors.White, 1200, 400);
        new ImageMagick.Drawing.Drawables().FontPointSize(64).FillColor(MagickColors.Black).Text(80, 180, "Jane Doe 221B Baker Street").Draw(card);
        var lines = await TextFinder.FindAsync(card, "eng", fx.NewFolder(), data, TestContext.Current.CancellationToken);
        var line = Assert.Single(lines);
        Assert.True(line.Y < 0.45 && line.Bottom > 0.35, line.ToString());

        using var empty = new MagickImage(MagickColors.White, 600, 400);
        Assert.Empty(await TextFinder.FindAsync(empty, "eng", fx.NewFolder(), data, TestContext.Current.CancellationToken));
    }
}
