// Morph change: the main colours of a picture, for the "Colour palette" file action (an idea from OrbitDrop, MIT,
// github.com/i3sam/OrbitDrop). The picture is made small, reduced to a few colours without dithering, and the colours
// that cover the most of its visible pixels win; transparent parts don't count, and near twins are kept once.

using ImageMagick;

namespace Filee.Engines.Magick;

public static class ColourPalette
{
    /// <summary>How many colours the palette has at most.</summary>
    public const int DefaultCount = 6;

    /// <summary>Colours closer than this (RGB distance) count as one.</summary>
    private const double MinDistance = 28;

    /// <summary>The main colours of <paramref name="picture"/> as #RRGGBB, the most used first.</summary>
    public static IReadOnlyList<string> Extract(IMagickImage<byte> picture, int count = DefaultCount)
    {
        using var image = picture.Clone();
        image.Resize(new MagickGeometry(160, 160) { Greater = true });
        image.Quantize(new QuantizeSettings { Colors = (uint)Math.Max(2, count * 2), DitherMethod = DitherMethod.No });

        var counts = new Dictionary<int, int>();
        using (var pixels = image.GetPixels())
        {
            var data = pixels.ToByteArray(PixelMapping.RGBA) ?? [];
            for (var i = 0; i + 3 < data.Length; i += 4)
            {
                if (data[i + 3] < 128)
                    continue; // transparent: not part of what one sees
                var rgb = data[i] << 16 | data[i + 1] << 8 | data[i + 2];
                counts[rgb] = counts.GetValueOrDefault(rgb) + 1;
            }
        }

        var chosen = new List<int>();
        foreach (var rgb in counts.OrderByDescending(c => c.Value).ThenBy(c => c.Key).Select(c => c.Key))
        {
            if (chosen.Any(c => Distance(c, rgb) < MinDistance))
                continue;
            chosen.Add(rgb);
            if (chosen.Count == count)
                break;
        }
        return [.. chosen.Select(c => $"#{c:X6}")];
    }

    private static double Distance(int a, int b)
    {
        var dr = (a >> 16 & 0xFF) - (b >> 16 & 0xFF);
        var dg = (a >> 8 & 0xFF) - (b >> 8 & 0xFF);
        var db = (a & 0xFF) - (b & 0xFF);
        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }
}
