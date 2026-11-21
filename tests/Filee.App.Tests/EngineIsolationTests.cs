// Morph change: engines in worker processes (Core/Conversion/Isolation, App/Services/EngineWorkerHost). These start the
// real Morph executable from the test output as the worker.

using Filee.App.Services;
using Filee.Core.Conversion;
using Filee.Core.Conversion.Isolation;
using Filee.Core.Presets;
using Filee.Engines;
using Filee.Engines.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace Filee.App.Tests;

public sealed class EngineIsolationTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("morph-isolation-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); }
        catch (IOException) { }
    }

    private static string Program => Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "Morph.exe" : "Morph");

    private readonly TestLog _log = new();

    private IsolatedStepRunner Runner(IReadOnlySet<string>? isolated = null, string? program = null, long memoryCeiling = 0,
        TimeSpan? stallLimit = null) =>
        new(program ?? Program, EngineWorkerHost.Arguments(Path.Combine(_dir, "data")), _log, isolated, maxWorkers: 2,
            memoryCeiling: memoryCeiling, stallLimit: stallLimit);

    /// <summary>The runner's log, shown when a test fails.</summary>
    private sealed class TestLog : Microsoft.Extensions.Logging.ILogger
    {
        public readonly System.Collections.Concurrent.ConcurrentQueue<string> Lines = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Enqueue($"{logLevel}: {formatter(state, exception)} {exception}");
        public override string ToString() => string.Join("\n", Lines);
    }

    private static IConverter Magick() => EngineRegistry.CreateAll(new EngineEnvironment(Path.Combine(Path.GetTempPath(), "morph-isolation-engines"))).Single(c => c.Id == "magick");

    private string Practice()
    {
        var path = Path.Combine(_dir, "practice.png");
        if (!File.Exists(path))
            File.Copy(Path.Combine(Repository(), "src", "Filee.App", "Assets", "Guide", "practice.png"), path);
        return path;
    }

    private static string Repository()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Filee.slnx")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    private ConversionStep Step(string input, string to = "jpg", Preset? preset = null, List<string>? notices = null) =>
        new(input, "png", to, preset ?? new Preset { Name = "Test", TargetFormat = to }, new FolderAllocator(Path.Combine(_dir, "out")),
            Directory.CreateTempSubdirectory("morph-isolation-work-").FullName, Notice: notices is null ? null : notices.Add);

    private sealed class FolderAllocator(string folder) : IOutputAllocator
    {
        private int _n;

        public string? Allocate(string extension, string? suffix = null)
        {
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, $"out{Interlocked.Increment(ref _n)}{suffix}.{extension}");
        }
    }

    /// <summary>An engine the worker has under this id ends the worker the hard way (MORPH_WORKER_TESTS=1).</summary>
    private sealed class Crashing(string id = EngineWorker.CrashForTests) : IConverter
    {
        public string Id => id;
        public string DisplayName => id == EngineWorker.CrashForTests ? "Crash" : id;
        public IReadOnlyList<ConversionEdge> Edges => [];
        public int MaxParallelism => 1;
        public EngineStatus GetStatus() => EngineStatus.Available();
        public Task<IReadOnlyList<string>> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Ran in the test process: it should have run in the worker.");
    }

    [Fact]
    public void Every_preset_reaches_the_worker_unchanged()
    {
        var presets = BuiltInData.CreatePresets();
        var edited = presets.First(p => p.Kind == PresetKind.Convert).Clone();
        edited.Image.HiddenAreas = [new HiddenArea { X = 0.1, Y = 0.2, Width = 0.3, Height = 0.4, Style = HideStyle.Blur }];
        edited.Media.Clip = new ClipEdit { Parts = [new ClipPart { Start = 1, End = 4.5 }], FadeIn = 0.5 };
        edited.Pdf.Password = "secret ✓ پاس";
        foreach (var preset in presets.Append(edited))
        {
            var json = WorkerProtocol.WritePreset(preset);
            Assert.Equal(json, WorkerProtocol.WritePreset(WorkerProtocol.ReadPreset(json)));
        }
        var back = WorkerProtocol.ReadPreset(WorkerProtocol.WritePreset(edited));
        Assert.Equal(0.3, back.Image.HiddenAreas![0].Width);
        Assert.Equal(4.5, back.Media.Clip!.Parts[0].End);
        Assert.Equal("secret ✓ پاس", back.Pdf.Password);
    }

    [Fact]
    public async Task A_step_runs_in_a_worker_that_is_reused()
    {
        Assert.True(File.Exists(Program), Program);
        using var runner = Runner();
        var magick = Magick();
        var first = await runner.RunAsync(magick, Step(Practice()), null, CancellationToken.None);
        var second = await runner.RunAsync(magick, Step(Practice(), "webp"), null, CancellationToken.None);

        Assert.EndsWith(".jpg", Assert.Single(first));
        Assert.EndsWith(".webp", Assert.Single(second));
        Assert.True(new FileInfo(first[0]).Length > 1000);
        Assert.True(new FileInfo(second[0]).Length > 1000);
        Assert.True(runner.Started == 1, _log.ToString());
        Assert.Equal(1, runner.LiveWorkers);
    }

    [Fact]
    public async Task A_name_in_any_script_arrives_intact()
    {
        using var runner = Runner();
        var source = Path.Combine(_dir, "تصویر 사진 照片.png");
        File.Copy(Practice(), source);
        var outputs = await runner.RunAsync(Magick(), Step(source), null, CancellationToken.None);
        Assert.True(File.Exists(Assert.Single(outputs)));
    }

    [Fact]
    public async Task An_engine_that_crashes_fails_only_its_file()
    {
        Environment.SetEnvironmentVariable("MORPH_WORKER_TESTS", "1");
        try
        {
            using var runner = Runner(new HashSet<string>(IsolatedStepRunner.InProcessEngines) { EngineWorker.CrashForTests });
            var refused = await Assert.ThrowsAsync<ConversionRefusedException>(() =>
                runner.RunAsync(new Crashing(), Step(Practice()), null, CancellationToken.None));
            Assert.Equal("error.engine_crashed", refused.ErrorKey);
            Assert.Contains("Crash", refused.Detail);

            // This process is still here, and the next file gets a new worker.
            var outputs = await runner.RunAsync(Magick(), Step(Practice()), null, CancellationToken.None);
            Assert.True(File.Exists(Assert.Single(outputs)));
            Assert.Equal(2, runner.Started);
        }
        finally
        {
            Environment.SetEnvironmentVariable("MORPH_WORKER_TESTS", null);
        }
    }

    [Theory]
    [InlineData(EngineWorker.HogForTests, "error.engine_memory")]
    [InlineData(EngineWorker.HangForTests, "error.engine_stalled")]
    public async Task A_worker_past_its_limits_is_stopped_and_only_its_file_fails(string engine, string key)
    {
        // Morph change: the memory ceiling and the stall limit.
        Environment.SetEnvironmentVariable("MORPH_WORKER_TESTS", "1");
        var every = IsolatedStepRunner.WatchEvery;
        IsolatedStepRunner.WatchEvery = TimeSpan.FromMilliseconds(250);
        try
        {
            using var runner = Runner(new HashSet<string>(IsolatedStepRunner.InProcessEngines) { engine },
                memoryCeiling: 600L << 20, stallLimit: TimeSpan.FromSeconds(3));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var refused = await Assert.ThrowsAsync<ConversionRefusedException>(() =>
                runner.RunAsync(new Crashing(engine), Step(Practice()), null, CancellationToken.None));
            Assert.Equal(key, refused.ErrorKey);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(40), watch.Elapsed.ToString());

            var outputs = await runner.RunAsync(Magick(), Step(Practice()), null, CancellationToken.None);
            Assert.True(File.Exists(Assert.Single(outputs)));
        }
        finally
        {
            IsolatedStepRunner.WatchEvery = every;
            Environment.SetEnvironmentVariable("MORPH_WORKER_TESTS", null);
        }
    }

    [Fact]
    public async Task Without_a_worker_the_step_runs_here_as_before()
    {
        using var runner = Runner(program: Path.Combine(_dir, "no-such-morph"));
        var outputs = await runner.RunAsync(Magick(), Step(Practice()), null, CancellationToken.None);
        Assert.True(File.Exists(Assert.Single(outputs)));
        Assert.Equal(0, runner.Started);
    }

    [Fact]
    public async Task Engines_that_run_their_own_program_stay_in_this_process()
    {
        using var runner = Runner(isolated: new HashSet<string>());
        var outputs = await runner.RunAsync(Magick(), Step(Practice()), null, CancellationToken.None);
        Assert.True(File.Exists(Assert.Single(outputs)));
        Assert.Equal(0, runner.Started);
        Assert.DoesNotContain("ffmpeg", IsolatedStepRunner.InProcessEngines);
        Assert.DoesNotContain("libreoffice", IsolatedStepRunner.InProcessEngines);
        Assert.Contains("magick", IsolatedStepRunner.InProcessEngines);
        Assert.Contains("pdfium", IsolatedStepRunner.InProcessEngines);
    }

    [Fact]
    public async Task A_whole_job_runs_through_the_queue_with_workers()
    {
        using var runner = Runner();
        var converters = EngineRegistry.CreateAll(new EngineEnvironment(Path.Combine(_dir, "data")));
        await using var queue = new JobQueue(new ConverterCatalog(converters)) { StepRunner = runner };
        var preset = new Preset
        {
            Name = "To JPG",
            TargetFormat = "jpg",
            Output = new OutputRule { Location = OutputLocation.SameFolder },
        };
        var job = queue.Enqueue([Practice()], preset, "To JPG");
        var finished = new TaskCompletionSource();
        queue.JobFinished += (_, j) => { if (ReferenceEquals(j, job)) finished.TrySetResult(); };
        if (job.State is JobState.Completed or JobState.CompletedWithErrors or JobState.Failed)
            finished.TrySetResult();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);

        Assert.Equal(JobState.Completed, job.State);
        var output = Assert.Single(job.Outputs);
        Assert.Equal(Path.Combine(_dir, "practice.jpg"), output);
        Assert.True(File.Exists(output));
        Assert.Equal(1, runner.Started);
    }
}
