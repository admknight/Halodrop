using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Filee.App.ViewModels;

namespace Filee.App.Views;

public partial class PresetEditorView : UserControl
{
    public PresetEditorView() => InitializeComponent();

    /// <summary>Morph change: the installed apps, read once when the list first opens (reading them takes a moment).</summary>
    private async void OnAppPickerOpened(object? sender, EventArgs e)
    {
        if (AppPicker.ItemsSource is not null)
            return;
        AppPicker.ItemsSource = Array.Empty<Filee.Core.Platform.InstalledApp>();
        var platform = Filee.App.Services.AppHost.Get<Filee.Core.Platform.IPlatformServices>();
        try
        {
            AppPicker.ItemsSource = await Task.Run(platform.InstalledApps);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            AppPicker.ItemsSource = null; // tried again the next time it opens
        }
    }

    private void OnAppPicked(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is PresetEditorViewModel vm && AppPicker.SelectedItem is Filee.Core.Platform.InstalledApp app)
            vm.PickApp(app);
    }

    /// <summary>Morph change: picks the program of "Run a program" (on a Mac an .app is a folder, chosen as one).</summary>
    private async void OnBrowseProgram(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PresetEditorViewModel vm || TopLevel.GetTopLevel(this) is not { } top)
            return;
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
            vm.RunProgram = path;
    }

    private async void OnBrowseFolder(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PresetEditorViewModel vm || TopLevel.GetTopLevel(this) is not { } top)
            return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (path is not null)
            vm.CustomFolder = path;
    }
}
