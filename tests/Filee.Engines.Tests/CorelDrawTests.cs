// Morph change: CorelDRAW drawings through MorphCdr (libcdr) and the vector engine, without LibreOffice. The real
// conversions run when the reader is built (build/build-cdr-reader.ps1, or engines/cdr on a developer's machine).

using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Engines.Vector;
using ImageMagick;

namespace Filee.Engines.Tests;

public class CorelDrawTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    private static string Drawing => Path.Combine(AppContext.BaseDirectory, "CorelDraw", "fdo65220-2.cdr");

    [Fact]
    public void A_drawing_has_one_cheap_edge_to_each_target_so_it_comes_before_LibreOffice()
    {
        var engine = new CorelDrawConverter();
        Assert.Contains(engine.Edges, e => e is { From: "cdr", To: "png" });
        Assert.Contains(engine.Edges, e => e is { From: "cdr", To: "pdf" });
        Assert.Contains(engine.Edges, e => e is { From: "cdr", To: "svg" });
        // Even with the largest priority penalty (9, an engine missing from an older priority list) it's cheaper than
        // LibreOffice's edge with its own (10 + 9).
        Assert.All(engine.Edges, e => Assert.True(e.Cost + 9 < 10 + 9, $"{e.From} → {e.To} costs {e.Cost}"));
    }

    [Fact]
    public void Pages_come_in_page_order()
    {
        var folder = fx.NewFolder();
        foreach (var n in new[] { 10, 2, 1 })
            File.WriteAllText(Path.Combine(folder, $"page-{n}.svg"), "<svg/>");
        Assert.Equal(["page-1.svg", "page-2.svg", "page-10.svg"], CorelDrawConverter.Pages(folder).Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("png")]
    [InlineData("pdf")]
    [InlineData("svg")]
    public async Task A_CorelDRAW_drawing_converts_without_LibreOffice(string target)
    {
        Assert.SkipUnless(new CorelDrawConverter().GetStatus().IsAvailable, "The CorelDRAW reader isn't built (build/build-cdr-reader.ps1)");
        var job = await fx.ConvertAsync([Drawing], new Preset { TargetFormat = target });
        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        var output = Assert.Single(job.Outputs);
        Assert.EndsWith("." + target, output);
        Assert.True(new FileInfo(output).Length > 200);
        if (target == "png")
        {
            using var image = new MagickImage(output);
            Assert.True(image.Width > 100 && image.Height > image.Width, $"{image.Width}x{image.Height}"); // an A4 page
        }
        if (target == "svg")
            Assert.Contains("<path", File.ReadAllText(output));
    }

    [Fact]
    public async Task A_file_that_isnt_a_drawing_is_refused_with_a_reason()
    {
        Assert.SkipUnless(new CorelDrawConverter().GetStatus().IsAvailable, "The CorelDRAW reader isn't built (build/build-cdr-reader.ps1)");
        var fake = Path.Combine(fx.NewFolder(), "not-a-drawing.cdr");
        File.WriteAllText(fake, "RIFF this is not CorelDRAW at all");
        var job = await fx.ConvertAsync([fake], new Preset { TargetFormat = "png" });
        var file = Assert.Single(job.Files);
        Assert.Equal(FileState.Failed, file.State);
    }
}
