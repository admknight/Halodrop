// Morph change: the guided first run on the Home page (WelcomeGuideViewModel).

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.App.ViewModels.Pages;
using Filee.App.Views;
using Filee.Core.Conversion;
using Filee.Core.Localization;
using Filee.Core.Platform;
using Filee.Core.Settings;

namespace Filee.App.Tests;

public class WelcomeGuideTests
{
    private static (WelcomeGuideViewModel Guide, UserDataStore Store, string Directory) NewGuide()
    {
        TestServices.EnsureInitialized("en");
        var directory = System.IO.Directory.CreateTempSubdirectory("morph-guide-").FullName;
        var store = new UserDataStore(directory);
        store.Load();
        return (new WelcomeGuideViewModel(store, AppHost.Get<ILocalizer>(), AppHost.Get<IPlatformServices>(), null, "hint"), store, directory);
    }

    [AvaloniaFact]
    public void A_new_user_sees_it_and_walks_through_four_steps()
    {
        var (guide, store, directory) = NewGuide();
        try
        {
            Assert.False(store.Settings.WelcomeGuideDone);
            Assert.True(guide.IsVisible);
            Assert.True(guide.IsWelcome);
            Assert.Equal("Step 1 of 4", guide.StepText);

            guide.StartCommand.Execute(null);
            Assert.True(guide.IsPractice);

            // The practice picture is copied out of the app into Morph's own folder, once.
            var practice = guide.PracticeFile();
            Assert.NotNull(practice);
            Assert.StartsWith(directory, practice);
            Assert.True(new FileInfo(practice).Length > 10_000);
            Assert.Equal(practice, guide.PracticeFile());

            // A cancelled donut keeps the practice step; a failed one says so; a finished one shows the result.
            guide.Finished(JobState.Cancelled, null);
            Assert.True(guide.IsPractice);
            Assert.False(guide.PracticeFailed);
            guide.Finished(JobState.Failed, null);
            Assert.True(guide.PracticeFailed);
            var output = Path.Combine(directory, "Morph-practice.jpg");
            guide.Finished(JobState.Completed, output);
            Assert.True(guide.IsDone);
            Assert.Contains("Morph-practice.jpg", guide.DoneText);

            guide.NextCommand.Execute(null);
            Assert.True(guide.IsEveryday);
            Assert.Equal("Step 4 of 4", guide.StepText);
            guide.FinishCommand.Execute(null);
            Assert.False(guide.IsVisible);
            Assert.True(store.Settings.WelcomeGuideDone);

            // Saved: a new Home page doesn't show it again, until it is asked for.
            var again = new WelcomeGuideViewModel(store, AppHost.Get<ILocalizer>(), AppHost.Get<IPlatformServices>(), null, "hint");
            Assert.False(again.IsVisible);
            again.Restart();
            Assert.True(again.IsVisible);
            Assert.True(again.IsWelcome);
            Assert.False(store.Settings.WelcomeGuideDone);
        }
        finally
        {
            System.IO.Directory.Delete(directory, true);
        }
    }

    [AvaloniaFact]
    public void Skip_ends_it_for_good()
    {
        var (guide, store, directory) = NewGuide();
        try
        {
            guide.SkipCommand.Execute(null);
            Assert.False(guide.IsVisible);
            Assert.True(store.Settings.WelcomeGuideDone);
        }
        finally
        {
            System.IO.Directory.Delete(directory, true);
        }
    }

    [AvaloniaFact]
    public void The_practice_picture_converts_for_real()
    {
        // The real conversion the practice step runs (PNG → JPG through the built-in engines), so the guide never
        // offers something that fails.
        var (guide, _, directory) = NewGuide();
        try
        {
            var practice = guide.PracticeFile()!;
            var conversions = AppHost.Get<ConversionService>();
            var preset = AppHost.Get<UserDataStore>().Presets.First(p => p.Kind == Filee.Core.Presets.PresetKind.Convert
                                                                         && string.Equals(p.TargetFormat, "jpg", StringComparison.OrdinalIgnoreCase));
            var run = conversions.RunAsync([practice], preset);
            var deadline = DateTime.UtcNow.AddSeconds(60);
            while (!run.IsCompleted && DateTime.UtcNow < deadline)
            {
                Dispatcher.UIThread.RunJobs();
                Thread.Sleep(20);
            }
            Assert.True(run.IsCompleted, "the practice conversion didn't finish");
            var job = run.Result!;
            Assert.Equal(JobState.Completed, job.State);
            var output = Assert.Single(job.Outputs);
            Assert.EndsWith(".jpg", output, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(output));
            File.Delete(output);
        }
        finally
        {
            System.IO.Directory.Delete(directory, true);
        }
    }

    [AvaloniaTheory]
    [InlineData("en")]
    [InlineData("ur")]
    public void The_home_page_shows_it_and_renders(string language)
    {
        TestServices.EnsureInitialized(language);
        var store = AppHost.Get<UserDataStore>();
        store.Settings.WelcomeGuideDone = false;
        using var vm = new MainWindowViewModel(AppHost.Services, AppHost.Get<ILocalizer>(), AppHost.Get<UpdateService>(), store);
        vm.Navigate("home");
        var window = new MainWindow { DataContext = vm, Width = 1080, Height = 820 };
        try
        {
            window.Show();
            for (var i = 0; i < 30; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            var home = Assert.IsType<HomePageViewModel>(vm.CurrentPage);
            Assert.True(home.Guide.IsVisible);
            home.Guide.StartCommand.Execute(null);
            for (var i = 0; i < 10; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            using var stream = File.Create(Path.Combine(TestServices.ScreenshotDirectory, $"guide-practice-{language}.png"));
            frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        finally
        {
            window.Close();
            store.Settings.WelcomeGuideDone = true;
            TestServices.EnsureInitialized("en");
            Dispatcher.UIThread.RunJobs();
        }
    }
}
