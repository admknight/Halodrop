// Morph change: CorelDRAW drawings (.cdr) without LibreOffice (an idea from OrbitDrop, MIT). MorphCdr, a small program
// built on libcdr (src/Morph.CdrReader, bundled under engines/cdr), writes each page as an SVG in a process of its own
// with a time limit, so a damaged or hostile drawing can only fail its own conversion. The pages then go through the
// built-in vector engine to the target (PNG, PDF, JPG ...) in the same step: one engine edge per target keeps this
// route ahead of LibreOffice's, which stays the fallback where MorphCdr isn't bundled.

using Filee.Core.Conversion;
using Filee.Engines.Infrastructure;
using Filee.Engines.Magick;

namespace Filee.Engines.Vector;

public sealed class CorelDrawConverter : IConverter
{
    public const string EngineId = "corel";

    /// <summary>A drawing that takes longer than this is taken as one MorphCdr can't read.</summary>
    internal static readonly TimeSpan TimeLimit = TimeSpan.FromMinutes(2);

    /// <summary>Below LibreOffice's edges (10), so a bundled reader is preferred.</summary>
    private const int Cost = 4;

    private readonly VectorConverter _vector = new();
    private string? _exe;

    public string Id => EngineId;
    public string DisplayName => "CorelDRAW reader (libcdr)";
    public int MaxParallelism => 2;

    public IReadOnlyList<ConversionEdge> Edges { get; } =
    [
        new("cdr", "svg", Cost),
        new("cdr", "pdf", Cost),
        .. ImageEncoder.Writable.Select(target => new ConversionEdge("cdr", target, Cost)),
    ];

    public EngineStatus GetStatus()
    {
        _exe = Locate();
        return _exe is null ? EngineStatus.Unavailable("engine.reason.not_installed") : EngineStatus.Available(_exe, "libcdr 0.1");
    }

    public async Task<IReadOnlyList<string>> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        var exe = _exe ?? Locate() ?? throw new InvalidOperationException("The CorelDRAW reader was not found.");
        var folder = Path.Combine(step.WorkDirectory, "cdr-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        ProcessResult result;
        try
        {
            result = await ProcessRunner.RunAsync(exe, [step.InputPath, folder], TimeLimit, cancellationToken);
        }
        catch (TimeoutException ex)
        {
            throw new ConversionRefusedException("error.cdr_unreadable", ex.Message);
        }
        if (result.ExitCode is 2 or 3)
            throw new ConversionRefusedException("error.cdr_unreadable", result.StandardError.Trim());
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"MorphCdr: {result.StandardError.Trim()} (exit code {result.ExitCode})");

        var pages = Pages(folder);
        if (pages.Count == 0)
            throw new ConversionRefusedException("error.cdr_unreadable", null);
        progress?.Report(0.3);

        var outputs = new List<string>();
        for (var i = 0; i < pages.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var suffix = pages.Count > 1 ? $"_p{i + 1}" : null;
            if (step.To == "svg")
            {
                if (step.Output.Allocate("svg", suffix) is { } path)
                {
                    File.Copy(pages[i], path, overwrite: true);
                    outputs.Add(path);
                }
            }
            else
            {
                var page = step with { InputPath = pages[i], From = "svg", Output = new PageOutput(step.Output, suffix) };
                outputs.AddRange(await _vector.ConvertAsync(page, null, cancellationToken));
            }
            progress?.Report(0.3 + 0.7 * (i + 1) / pages.Count);
        }
        return outputs;
    }

    /// <summary>The page SVGs MorphCdr wrote, in page order.</summary>
    internal static List<string> Pages(string folder) =>
    [
        .. Directory.GetFiles(folder, "page-*.svg")
            .Select(p => (Path: p, Number: int.TryParse(Path.GetFileNameWithoutExtension(p)["page-".Length..], out var n) ? n : int.MaxValue))
            .OrderBy(p => p.Number).Select(p => p.Path),
    ];

    /// <summary>A page's outputs named after it ("drawing_p2.png") when the drawing has several.</summary>
    private sealed class PageOutput(IOutputAllocator inner, string? page) : IOutputAllocator
    {
        public string? Allocate(string extension, string? suffix = null) => inner.Allocate(extension, page + suffix);
    }

    /// <summary>Morph's own copy (engines/cdr), never one found elsewhere.</summary>
    private static string? Locate() =>
        EngineEnvironment.FindBundled("cdr") is { } bundled
            ? EngineEnvironment.FirstExisting(Path.Combine(bundled, OperatingSystem.IsWindows() ? "MorphCdr.exe" : "MorphCdr"))
            : null;
}
