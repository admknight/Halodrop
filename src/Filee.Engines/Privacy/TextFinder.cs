// Morph change: lines of text in a picture, for the Hide window's "Find text" (automatic text blur: names, plates,
// addresses on a screenshot). Tesseract, which Morph already uses for OCR, reports where each line it reads is; nothing
// leaves the computer. A line counts when at least one word on it was read with some confidence.

using System.Globalization;
using Filee.Engines.Infrastructure;
using Filee.Engines.Ocr;
using ImageMagick;

namespace Filee.Engines.Privacy;

public static class TextFinder
{
    private static readonly TimeSpan TimeLimit = TimeSpan.FromMinutes(2);

    /// <summary>Tesseract's confidence (0 to 100) a word needs for its line to count.</summary>
    private const double MinConfidence = 40;

    public static bool IsAvailable => TesseractConverter.Locate() is not null;

    /// <summary>
    /// The lines of text in <paramref name="picture"/>, in the languages of <paramref name="languageSetting"/> (the
    /// "Searchable PDF" setting); <paramref name="languageData"/> gives each language's data file (downloads it).
    /// </summary>
    public static async Task<IReadOnlyList<FoundArea>> FindAsync(IMagickImage<byte> picture, string? languageSetting, string workRoot,
        Func<string, CancellationToken, Task<string>>? languageData = null, CancellationToken cancellationToken = default)
    {
        var tesseract = TesseractConverter.Locate() ?? throw new InvalidOperationException("Tesseract is not installed.");
        var languages = OcrLanguages.Parse(languageSetting);
        var data = await TesseractConverter.LanguageDataAsync(languages, languageData ?? OcrLanguages.EnsureAsync, cancellationToken);
        var scratch = Path.Combine(workRoot, "find-text-" + Guid.NewGuid().ToString("N")[..8]);
        var tessdata = Path.Combine(scratch, "tessdata");
        Directory.CreateDirectory(tessdata);
        try
        {
            foreach (var file in data)
                File.Copy(file, Path.Combine(tessdata, Path.GetFileName(file)));
            using (var copy = picture.Clone())
            {
                copy.Alpha(AlphaAction.Remove);
                await copy.WriteAsync(Path.Combine(scratch, "picture.png"), MagickFormat.Png, cancellationToken);
            }
            // tessedit_create_tsv instead of the "tsv" config file, which lives in Tesseract's own tessdata folder.
            var result = await ProcessRunner.RunAsync(tesseract,
                ["picture.png", "found", "--tessdata-dir", "tessdata", "-l", OcrLanguages.Join(languages.Select(l => l.Code)), "-c", "tessedit_create_tsv=1"],
                TimeLimit, cancellationToken, workingDirectory: scratch,
                environment: new Dictionary<string, string> { ["OMP_THREAD_LIMIT"] = "2", ["TESSDATA_PREFIX"] = "" });
            var tsv = Path.Combine(scratch, "found.tsv");
            if (result.ExitCode != 0 || !File.Exists(tsv))
                throw new InvalidOperationException($"Tesseract failed (exit {result.ExitCode}). {result.StandardError.Trim()}");
            return Lines(await File.ReadAllTextAsync(tsv, cancellationToken), (int)picture.Width, (int)picture.Height);
        }
        finally
        {
            try { Directory.Delete(scratch, recursive: true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    /// <summary>The lines of Tesseract's TSV that hold a word read with confidence, a little larger than the text.</summary>
    internal static IReadOnlyList<FoundArea> Lines(string tsv, int width, int height)
    {
        var lines = new Dictionary<(int Block, int Paragraph, int Line), (int Left, int Top, int Right, int Bottom)>();
        foreach (var row in tsv.Split('\n').Skip(1))
        {
            var c = row.TrimEnd('\r').Split('\t');
            if (c.Length < 12 || c[0] != "5" || string.IsNullOrWhiteSpace(c[11]))
                continue;
            if (!double.TryParse(c[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var confidence) || confidence < MinConfidence)
                continue;
            int N(int i) => int.Parse(c[i], CultureInfo.InvariantCulture);
            var key = (N(2), N(3), N(4));
            var (left, top, right, bottom) = (N(6), N(7), N(6) + N(8), N(7) + N(9));
            lines[key] = lines.TryGetValue(key, out var line)
                ? (Math.Min(line.Left, left), Math.Min(line.Top, top), Math.Max(line.Right, right), Math.Max(line.Bottom, bottom))
                : (left, top, right, bottom);
        }
        return
        [
            .. lines.Values.Select(l => new FoundArea((double)l.Left / width, (double)l.Top / height,
                (double)(l.Right - l.Left) / width, (double)(l.Bottom - l.Top) / height).Grow(0.08)),
        ];
    }
}
