// Morph change: ideas from OrbitDrop (MIT) as Morph does them. Social sizes fill the box and crop the middle instead of
// squashing; "Colour palette" finds a picture's main colours; "Normalize volume" brings a quiet sound up without
// clipping (a real run needs FFmpeg, made on a tone FFmpeg makes itself).

using System.Globalization;
using Filee.Core.Conversion;
using Filee.Core.Presets;
using Filee.Engines.Infrastructure;
using Filee.Engines.Magick;
using Filee.Engines.Media;
using ImageMagick;
using ImageMagick.Drawing;

namespace Filee.Engines.Tests;

[Collection(nameof(FfmpegCollection))]
public class MorphOrbitDropIdeasTests(EngineFixture fx) : IClassFixture<EngineFixture>
{
    [Theory]
    [InlineData(4000, 3000, 1080, 1080)]
    [InlineData(3000, 4000, 1080, 1920)]
    [InlineData(800, 600, 1280, 720)]
    [InlineData(1080, 1080, 1080, 1080)]
    public void A_social_size_fills_the_box_and_crops_the_middle(int width, int height, int boxWidth, int boxHeight)
    {
        // Left half red, right half blue: a centred crop keeps both, a squash would keep both too but bend the circle.
        using var image = new MagickImage(MagickColors.Red, (uint)width, (uint)height);
        new Drawables().FillColor(MagickColors.Blue).Rectangle(width / 2.0, 0, width, height).Draw(image);
        var options = new ImageOptions { CustomSize = new CustomImageSize { Width = boxWidth, Height = boxHeight, Fill = true } };

        ImageEncoder.ApplyOptions(image, options, "jpg");

        Assert.Equal(((uint)boxWidth, (uint)boxHeight), (image.Width, image.Height));
        Assert.Equal((0, 0), (image.Page.X, image.Page.Y));
        using var pixels = image.GetPixels();
        var left = pixels.GetPixel(2, (int)image.Height / 2)!.ToColor()!;
        var right = pixels.GetPixel((int)image.Width - 3, (int)image.Height / 2)!.ToColor()!;
        Assert.True(left.R > 200 && left.B < 60, $"left {left}");
        Assert.True(right.B > 200 && right.R < 60, $"right {right}");
    }

    [Fact]
    public void A_squashing_size_still_stretches_when_fill_is_off()
    {
        using var image = new MagickImage(MagickColors.Red, 400, 100);
        ImageEncoder.ApplyOptions(image, new ImageOptions { CustomSize = new CustomImageSize { Width = 100, Height = 100 } }, "png");
        Assert.Equal((100u, 100u), (image.Width, image.Height));
    }

    [Fact]
    public void The_built_in_social_sizes_are_jpgs_that_fill()
    {
        var social = BuiltInData.SocialPresets().Where(p => !p.IsGroup).ToList();
        Assert.Equal(3, social.Count);
        Assert.All(social, p =>
        {
            Assert.Equal("jpg", p.TargetFormat);
            Assert.True(p.Image.CustomSize is { Fill: true, LockAspect: false });
        });
    }

    [Fact]
    public void The_palette_has_the_main_colours_most_used_first_and_skips_transparency()
    {
        // 60 % green, 30 % orange, 10 % transparent (which must not count, though it is "black" underneath).
        using var image = new MagickImage(MagickColors.Transparent, 100, 100);
        new Drawables().FillColor(new MagickColor("#22AA44")).Rectangle(0, 0, 59, 99).Draw(image);
        new Drawables().FillColor(new MagickColor("#FF8800")).Rectangle(60, 0, 89, 99).Draw(image);

        var colours = ColourPalette.Extract(image);

        Assert.Equal(2, colours.Count);
        Assert.True(Near(colours[0], "#22AA44"), colours[0]);
        Assert.True(Near(colours[1], "#FF8800"), colours[1]);
        Assert.All(colours, c => Assert.Matches("^#[0-9A-F]{6}$", c));
    }

    [Fact]
    public void A_busy_picture_gives_at_most_six_distinct_colours()
    {
        using var image = new MagickImage("plasma:", 200, 200);
        var colours = ColourPalette.Extract(image);
        Assert.InRange(colours.Count, 1, ColourPalette.DefaultCount);
        Assert.Equal(colours.Count, colours.Distinct().Count());
    }

    [Fact]
    public void Normalize_encodes_the_sound_with_the_loudness_filter_at_the_sources_rate()
    {
        var options = new MediaOptions { NormalizeLoudness = true, CopyAudioWhenPossible = true };
        var audio = new AudioStreamInfo(0, 44100, "mp3", 2);
        Assert.False(MediaEncoding.CanCopyIntoAudio("mp3", audio, options));
        Assert.False(MediaEncoding.CanCopyAudio("mp3", audio, options));

        var args = MediaEncoding.NormalizeArguments(["-c:a", "libmp3lame"], options, 44100).ToList();
        Assert.Equal(["-af", MediaEncoding.LoudnessFilter, "-ar", "44100"], args);
        // A rate the preset already sets is kept.
        Assert.Equal(["-af", MediaEncoding.LoudnessFilter], MediaEncoding.NormalizeArguments(["-ar", "22050"], options, 44100));
        Assert.Empty(MediaEncoding.NormalizeArguments([], new MediaOptions(), 44100));
    }

    [Fact]
    public async Task A_quiet_recording_comes_out_louder_without_clipping()
    {
        Assert.SkipUnless(Tools is not null, "FFmpeg is not installed (pwsh build/fetch-engines.ps1 -Only ffmpeg)");
        var quiet = Path.Combine(fx.NewFolder(), "quiet.wav");
        var made = await ProcessRunner.RunAsync(Tools!.Value.Ffmpeg, ["-hide_banner", "-nostdin", "-v", "error", "-y",
            "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100:duration=4", "-af", "volume=-30dB", "-c:a", "pcm_s16le", quiet],
            TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.True(made.ExitCode == 0, made.StandardError);

        var job = await fx.ConvertAsync([quiet], BuiltInData.NormalizeAudioPreset());
        Assert.True(job.State == JobState.Completed, string.Join("; ", job.Files.Select(f => $"{f.ErrorKey} {f.ErrorDetail}")));
        var output = Assert.Single(job.Outputs);
        Assert.EndsWith("quiet_normalized.wav", output);

        var before = await PeakAsync(quiet);
        var after = await PeakAsync(output);
        Assert.True(after > before + 15, $"{before} dB → {after} dB");
        Assert.True(after <= -1.0, $"peak {after} dB");
        Assert.Equal("44100", await SampleRateAsync(output));
    }

    private static readonly (string Ffmpeg, string Ffprobe)? Tools = FfmpegConverter.Locate();

    private static bool Near(string a, string b)
    {
        int Part(string hex, int i) => int.Parse(hex.AsSpan(1 + i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Enumerable.Range(0, 3).All(i => Math.Abs(Part(a, i) - Part(b, i)) <= 24);
    }

    /// <summary>The loudest sample in dB (FFmpeg's volumedetect).</summary>
    private static async Task<double> PeakAsync(string path)
    {
        var result = await ProcessRunner.RunAsync(Tools!.Value.Ffmpeg, ["-hide_banner", "-nostdin", "-i", path, "-af", "volumedetect", "-f", "null", "-"],
            TimeSpan.FromMinutes(1), CancellationToken.None);
        var line = result.StandardError.Split('\n').Single(l => l.Contains("max_volume:", StringComparison.Ordinal));
        var value = line[(line.IndexOf("max_volume:", StringComparison.Ordinal) + 11)..].Replace("dB", "", StringComparison.Ordinal).Trim();
        return double.Parse(value, CultureInfo.InvariantCulture);
    }

    private static async Task<string> SampleRateAsync(string path)
    {
        var result = await ProcessRunner.RunAsync(Tools!.Value.Ffprobe, ["-v", "error", "-select_streams", "a:0", "-show_entries", "stream=sample_rate",
            "-of", "default=nw=1:nk=1", path], TimeSpan.FromMinutes(1), CancellationToken.None);
        return result.StandardOutput.Trim();
    }
}
