// Morph change: conversions that didn't finish are offered again (UnfinishedWork), and a file Morph stopped on twice
// is left out.

using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Filee.App.Services;
using Filee.App.Views;
using Filee.Core.Conversion;
using Filee.Core.Localization;
using Filee.Core.Presets;

namespace Filee.App.Tests;

public sealed class UnfinishedWorkTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("morph-unfinished-").FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); }
        catch (IOException) { }
    }

    /// <summary>PNG → JPG that holds each file until released, so a job can be caught half-way.</summary>
    private sealed class Holding : IConverter
    {
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string Id => "holding";
        public string DisplayName => "Holding";
        public IReadOnlyList<ConversionEdge> Edges => [new("png", "jpg", 10)];
        public int MaxParallelism => 1;
        public EngineStatus GetStatus() => EngineStatus.Available();

        public async Task<IReadOnlyList<string>> ConvertAsync(ConversionStep step, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            await Release.Task.WaitAsync(cancellationToken);
            var output = step.Output.Allocate("jpg")!;
            File.Copy(step.InputPath, output);
            return [output];
        }
    }

    private string[] Sources()
    {
        var files = new[] { Path.Combine(_dir, "a.png"), Path.Combine(_dir, "b.png") };
        foreach (var file in files)
            if (!File.Exists(file))
                File.WriteAllBytes(file, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        return files;
    }

    private static Preset ToJpg() => new() { Name = "To JPG", TargetFormat = "jpg", Output = new OutputRule { Location = OutputLocation.SameFolder } };

    private string Journal => Path.Combine(_dir, "unfinished.json");

    /// <summary>Starts a job and waits until its first file is being converted; the job then hangs, as in a crash.</summary>
    private async Task<(JobQueue Queue, Holding Engine, ConversionJob Job, UnfinishedWork Work)> RunUntilStuckAsync()
    {
        var engine = new Holding();
        var queue = new JobQueue(new ConverterCatalog([engine]));
        var work = new UnfinishedWork(_dir, queue);
        var job = queue.Enqueue(Sources(), ToJpg(), "To JPG");
        for (var i = 0; i < 200 && !(File.Exists(Journal) && File.ReadAllText(Journal).Contains("\"running\": \"", StringComparison.Ordinal)); i++)
            await Task.Delay(25);
        return (queue, engine, job, work);
    }

    [Fact]
    public async Task A_job_that_did_not_finish_is_offered_again()
    {
        var (queue, engine, _, work) = await RunUntilStuckAsync();
        Assert.Contains("a.png", File.ReadAllText(Journal));

        // Morph "stops" here; the next start reads what is left.
        var next = new UnfinishedWork(_dir, null);
        var resume = Assert.Single(next.ToResume());
        Assert.Equal(Sources(), resume.Files);
        Assert.Equal("jpg", UnfinishedWork.PresetOf(resume.Job).TargetFormat);
        Assert.Empty(next.LeftOut);

        next.Forget();
        Assert.Empty(new UnfinishedWork(_dir, null).ToResume());

        engine.Release.TrySetResult();
        work.Dispose();
        await queue.DisposeAsync();
    }

    [Fact]
    public async Task A_file_that_stopped_Morph_twice_is_left_out()
    {
        for (var crash = 1; crash <= 2; crash++)
        {
            var (queue, _, _, work) = await RunUntilStuckAsync();
            work.Dispose(); // Morph "stops" with a.png being converted
            var next = new UnfinishedWork(_dir, null);
            if (crash == 1)
            {
                Assert.Equal(2, next.ToResume().Single().Files.Count);
                next.Forget();
            }
            else
            {
                Assert.Equal(Path.Combine(_dir, "a.png"), Assert.Single(next.LeftOut));
                Assert.Equal([Path.Combine(_dir, "b.png")], next.ToResume().Single().Files);
            }
            _ = queue.DisposeAsync();
        }
    }

    [Fact]
    public async Task A_start_after_a_good_run_does_not_count_the_same_file_again()
    {
        var (queue, _, _, work) = await RunUntilStuckAsync();
        work.Dispose();
        // Read at three starts in a row without an answer: still one stop on a.png, nothing left out.
        for (var i = 0; i < 3; i++)
            Assert.Empty(new UnfinishedWork(_dir, null).LeftOut);
        _ = queue.DisposeAsync();
    }

    [Fact]
    public async Task A_job_that_finishes_leaves_nothing()
    {
        var (queue, engine, job, work) = await RunUntilStuckAsync();
        var finished = new TaskCompletionSource();
        queue.JobFinished += (_, j) => { if (ReferenceEquals(j, job)) finished.TrySetResult(); };
        engine.Release.TrySetResult();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(JobState.Completed, job.State);
        Assert.False(File.Exists(Journal));
        work.Dispose();
        await queue.DisposeAsync();
    }

    [AvaloniaTheory]
    [InlineData("en")]
    [InlineData("ur")]
    public async Task The_card_offers_them_and_names_what_it_left_out(string language)
    {
        for (var crash = 1; crash <= 2; crash++)
        {
            var (queue, _, _, work) = await RunUntilStuckAsync();
            work.Dispose();
            _ = queue.DisposeAsync();
            if (crash == 1)
                new UnfinishedWork(_dir, null).Forget();
        }
        TestServices.EnsureInitialized(language);
        var resumed = new List<UnfinishedWork.Resumable>();
        var card = new ResumeNoticeWindow(new UnfinishedWork(_dir, null), AppHost.Get<ILocalizer>(), resumed.Add);
        try
        {
            card.Show();
            for (var i = 0; i < 20; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            using var frame = card.CaptureRenderedFrame();
            Assert.NotNull(frame);
            using (var stream = File.Create(Path.Combine(TestServices.ScreenshotDirectory, $"resume-{language}.png")))
                frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            card.FindControl<Avalonia.Controls.Button>("AgainButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Assert.Equal([Path.Combine(_dir, "b.png")], Assert.Single(resumed).Files);
            Assert.Empty(new UnfinishedWork(_dir, null).ToResume());
        }
        finally
        {
            card.Close();
            TestServices.EnsureInitialized("en");
        }
    }
}
