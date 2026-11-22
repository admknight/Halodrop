// Morph change: faces in a picture, for the Hide window's "Find faces" (automatic face blur, an idea from OrbitDrop).
// UltraFace (version-RFB-320, MIT, github.com/Linzaer/Ultra-Light-Fast-Generic-Face-Detector-1MB), a 1.2 MB detector
// run with ONNX Runtime on the computer: nothing leaves it. The picture is read whole and, when it's large, also in
// overlapping quarters so small faces in a group photo are found; the same face found twice is kept once.

using ImageMagick;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Filee.Engines.Privacy;

public static class FaceFinder
{
    private const int InputWidth = 320, InputHeight = 240;

    /// <summary>How sure the detector must be that it's a face.</summary>
    public const float DefaultThreshold = 0.7f;

    private static readonly Lock Gate = new();
    private static InferenceSession? _session;

    public static string ModelPath => Path.Combine(AppContext.BaseDirectory, "Privacy", "Models", "ultraface-rfb-320.onnx");

    public static bool IsAvailable => File.Exists(ModelPath);

    /// <summary>The faces in <paramref name="picture"/>, each a little larger than the face (hair, chin).</summary>
    public static IReadOnlyList<FoundArea> Find(IMagickImage<byte> picture, float threshold = DefaultThreshold)
    {
        var found = new List<(FoundArea Area, float Score)>();
        found.AddRange(Detect(picture, new FoundArea(0, 0, 1, 1), threshold));
        // Small faces are a few pixels at 320 x 240: a large picture is also read in four overlapping parts.
        if (Math.Max(picture.Width, picture.Height) > 1000)
        {
            foreach (var (x, y) in new[] { (0.0, 0.0), (0.45, 0.0), (0.0, 0.45), (0.45, 0.45) })
                found.AddRange(Detect(picture, new FoundArea(x, y, 0.55, 0.55), threshold));
        }
        return [.. KeepBest(found, 0.3).Select(a => a.Grow(0.15))];
    }

    private static IEnumerable<(FoundArea, float)> Detect(IMagickImage<byte> picture, FoundArea part, float threshold)
    {
        using var image = picture.Clone();
        if (part.Width < 1 || part.Height < 1)
        {
            image.Crop(new MagickGeometry((int)(part.X * picture.Width), (int)(part.Y * picture.Height),
                (uint)Math.Max(1, part.Width * picture.Width), (uint)Math.Max(1, part.Height * picture.Height)));
            image.ResetPage();
        }
        image.Alpha(AlphaAction.Remove);
        image.Resize(new MagickGeometry(InputWidth, InputHeight) { IgnoreAspectRatio = true });
        using var pixels = image.GetPixels();
        var rgb = pixels.ToByteArray(PixelMapping.RGB) ?? [];

        // NCHW, (value - 127) / 128, as the model was trained.
        var input = new DenseTensor<float>([1, 3, InputHeight, InputWidth]);
        for (var y = 0; y < InputHeight; y++)
        {
            for (var x = 0; x < InputWidth; x++)
            {
                var i = (y * InputWidth + x) * 3;
                for (var c = 0; c < 3; c++)
                    input[0, c, y, x] = (rgb[i + c] - 127f) / 128f;
            }
        }

        List<(FoundArea, float)> faces = [];
        lock (Gate)
        {
            var session = _session ??= new InferenceSession(ModelPath, new SessionOptions { IntraOpNumThreads = 2, LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR });
            var name = session.InputMetadata.Keys.First();
            using var results = session.Run([NamedOnnxValue.CreateFromTensor(name, input)]);
            var scores = results.First(r => r.Name == "scores").AsTensor<float>();
            var boxes = results.First(r => r.Name == "boxes").AsTensor<float>();
            var count = scores.Dimensions[1];
            for (var i = 0; i < count; i++)
            {
                var score = scores[0, i, 1];
                if (score < threshold)
                    continue;
                // Corners in parts of the input, then of the whole picture.
                double x1 = Math.Clamp(boxes[0, i, 0], 0, 1), y1 = Math.Clamp(boxes[0, i, 1], 0, 1);
                double x2 = Math.Clamp(boxes[0, i, 2], 0, 1), y2 = Math.Clamp(boxes[0, i, 3], 0, 1);
                if (x2 <= x1 || y2 <= y1)
                    continue;
                faces.Add((new FoundArea(part.X + x1 * part.Width, part.Y + y1 * part.Height, (x2 - x1) * part.Width, (y2 - y1) * part.Height), score));
            }
        }
        return faces;
    }

    /// <summary>The surest of overlapping finds (non-maximum suppression).</summary>
    internal static List<FoundArea> KeepBest(IEnumerable<(FoundArea Area, float Score)> found, double overlap)
    {
        var kept = new List<FoundArea>();
        foreach (var (area, _) in found.OrderByDescending(f => f.Score))
        {
            if (kept.All(k => k.Overlap(area) < overlap))
                kept.Add(area);
        }
        return kept;
    }
}
