// Morph change: Urdu's Nastaliq letters stand far taller than Latin ones. These render the main pages in Urdu (and in
// the Futuristic look) so a fixed height that clips them shows up in the screenshots, and check the reading direction.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.App.Views;
using Filee.Core.Localization;
using Filee.Core.Settings;

namespace Filee.App.Tests;

public class UrduLayoutTests
{
    [AvaloniaTheory]
    [InlineData("home", ThemeStyle.Soft)]
    [InlineData("general", ThemeStyle.Soft)]
    [InlineData("engines", ThemeStyle.Futuristic)]
    [InlineData("about", ThemeStyle.Futuristic)]
    public void The_main_window_renders_in_urdu(string page, ThemeStyle style)
    {
        TestServices.EnsureInitialized("ur");
        var store = AppHost.Get<UserDataStore>();
        var theme = AppHost.Get<ThemeService>();
        var previous = store.Settings.Theme.Style;
        store.Settings.Theme.Style = style;
        theme.Apply(store.Settings.Theme, "ur");
        using var vm = new MainWindowViewModel(AppHost.Services, AppHost.Get<ILocalizer>(), AppHost.Get<UpdateService>(), AppHost.Get<UserDataStore>());
        vm.Navigate(page);
        var window = new MainWindow { DataContext = vm, Width = 1080, Height = 820 };
        try
        {
            theme.ApplyWindowClass(window);
            window.Show();
            for (var i = 0; i < 30; i++)
            {
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            }
            Assert.Equal(FlowDirection.RightToLeft, window.FlowDirection);
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            using var stream = File.Create(Path.Combine(TestServices.ScreenshotDirectory, $"urdu-{page}-{style}.png".ToLowerInvariant()));
            frame.Save(stream, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        }
        finally
        {
            window.Close();
            store.Settings.Theme.Style = previous;
            TestServices.EnsureInitialized("en");
            Dispatcher.UIThread.RunJobs();
        }
    }
}
