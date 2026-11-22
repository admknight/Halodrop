// Morph change: the guided first run on the Home page. Four short steps a child can follow: what Morph does, a
// practice conversion of a picture Morph brings along (through the real donut, so it is the real thing), the result,
// and how to do it from the file manager. Skippable at any step; Settings → General brings it back.

using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Filee.Core.Conversion;
using Filee.Core.Localization;
using Filee.Core.Platform;
using Filee.Core.Settings;

namespace Filee.App.ViewModels;

/// <summary>Where the guide is.</summary>
public enum GuideStep
{
    Welcome,
    Practice,
    Done,
    Everyday,
}

public sealed partial class WelcomeGuideViewModel : ObservableObject, IDisposable
{
    /// <summary>The practice picture, an asset copied out the first time it is needed.</summary>
    internal const string PracticeAsset = "avares://Halodrop/Assets/Guide/practice.png";

    /// <summary>The practice picture's file name, the same in every language (the picture has no words).</summary>
    internal const string PracticeFileName = "Halodrop-practice.png";

    private readonly UserDataStore _store;
    private readonly ILocalizer _loc;
    private readonly IPlatformServices _platform;
    private readonly JobQueue? _queue;
    private ConversionJob? _practiceJob;

    public WelcomeGuideViewModel(UserDataStore store, ILocalizer loc, IPlatformServices platform, JobQueue? queue, string everydayHint)
    {
        _store = store;
        _loc = loc;
        _platform = platform;
        _queue = queue;
        EverydayHint = everydayHint;
        IsVisible = !store.Settings.WelcomeGuideDone;
        if (queue is not null)
            queue.JobFinished += OnJobFinished;
    }

    public void Dispose()
    {
        if (_queue is not null)
            _queue.JobFinished -= OnJobFinished;
    }

    [ObservableProperty] private bool _isVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcome), nameof(IsPractice), nameof(IsDone), nameof(IsEveryday), nameof(StepText))]
    private GuideStep _step = GuideStep.Welcome;

    public bool IsWelcome => Step == GuideStep.Welcome;
    public bool IsPractice => Step == GuideStep.Practice;
    public bool IsDone => Step == GuideStep.Done;
    public bool IsEveryday => Step == GuideStep.Everyday;

    /// <summary>"Step 2 of 4".</summary>
    public string StepText => _loc.Format("guide.step", (int)Step + 1, 4);

    /// <summary>How to open the donut from the file manager, with this computer's key (the Home page's own hint).</summary>
    public string EverydayHint { get; }

    /// <summary>The practice conversion failed: say so and offer to try again.</summary>
    [ObservableProperty] private bool _practiceFailed;

    /// <summary>The converted practice file (the first output).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DoneText))]
    private string? _result;

    /// <summary>"You made Morph-practice.jpg."</summary>
    public string DoneText => _loc.Format("guide.text_done", Result is null ? "" : Path.GetFileName(Result));

    [RelayCommand]
    private void Start() => Step = GuideStep.Practice;

    /// <summary>Skips the rest; Settings → General → Show the welcome guide brings it back.</summary>
    [RelayCommand]
    private void Skip() => Finish();

    [RelayCommand]
    private void Next() => Step = GuideStep.Everyday;

    [RelayCommand]
    private void Finish()
    {
        IsVisible = false;
        if (!_store.Settings.WelcomeGuideDone)
        {
            _store.Settings.WelcomeGuideDone = true;
            _store.SaveSettings();
        }
    }

    /// <summary>Shows the converted file in the file manager.</summary>
    [RelayCommand]
    private void ShowResult()
    {
        if (Result is { } path && (File.Exists(path) || Directory.Exists(path)))
            _platform.RevealInFileManager(path);
    }

    /// <summary>Opens the guide again from its first step (Settings → General).</summary>
    public void Restart()
    {
        _store.Settings.WelcomeGuideDone = false;
        _store.SaveSettings();
        Step = GuideStep.Welcome;
        Result = null;
        PracticeFailed = false;
        IsVisible = true;
    }

    /// <summary>
    /// The practice picture on disk (in Morph's own folder, never among the user's files), copied out of the app the
    /// first time. Null when it can't be written.
    /// </summary>
    public string? PracticeFile()
    {
        try
        {
            var folder = Path.Combine(_store.Directory, "practice");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, PracticeFileName);
            if (!File.Exists(path))
            {
                using var asset = AssetLoader.Open(new Uri(PracticeAsset));
                using var file = File.Create(path);
                asset.CopyTo(file);
            }
            return path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The donut started a conversion of the practice picture: wait for it.</summary>
    public void PracticeStarted(ConversionJob job)
    {
        _practiceJob = job;
        PracticeFailed = false;
    }

    private void OnJobFinished(object? sender, ConversionJob job)
    {
        if (!ReferenceEquals(job, _practiceJob))
            return;
        Dispatcher.UIThread.Post(() => Finished(job.State, job.Outputs.FirstOrDefault()));
    }

    /// <summary>The practice conversion ended: on to the result, or back to try again.</summary>
    internal void Finished(JobState state, string? output)
    {
        _practiceJob = null;
        if (state is JobState.Completed or JobState.CompletedWithErrors && output is not null)
        {
            Result = output;
            Step = GuideStep.Done;
        }
        else if (state != JobState.Cancelled)
        {
            PracticeFailed = true;
        }
    }
}
