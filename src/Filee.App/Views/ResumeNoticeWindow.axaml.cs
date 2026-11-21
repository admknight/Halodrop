// Morph change: offers the conversions the last run didn't finish (UnfinishedWork) again, and names any file left out
// because Morph stopped while converting it twice.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Filee.App.Services;
using Filee.Core.Localization;

namespace Filee.App.Views;

public partial class ResumeNoticeWindow : Window
{
    private const int ScreenMargin = 12;
    private readonly UnfinishedWork? _work;
    private readonly Action<UnfinishedWork.Resumable>? _resume;

    /// <summary>For the XAML previewer.</summary>
    public ResumeNoticeWindow() : this(null, null, null)
    {
    }

    public ResumeNoticeWindow(UnfinishedWork? work, ILocalizer? loc, Action<UnfinishedWork.Resumable>? resume)
    {
        _work = work;
        _resume = resume;
        InitializeComponent();
        Motion.Track(this);
        SizeChanged += (_, _) => PlaceBottomRight();
        if (work is null || loc is null)
            return;
        var files = work.ToResume().Sum(r => r.Files.Count);
        Summary.Text = loc.Format("resume.text", files);
        AgainButton.IsVisible = files > 0;
        if (work.LeftOut is { Count: > 0 } leftOut)
        {
            LeftOutText.Text = loc.Format("resume.left_out", string.Join(", ", leftOut.Select(Path.GetFileName)));
            LeftOutText.IsVisible = true;
        }
    }

    private void OnAgain(object? sender, RoutedEventArgs e)
    {
        if (_work is not null && _resume is not null)
            foreach (var job in _work.ToResume())
                _resume(job);
        _work?.Forget();
        Close();
    }

    private void OnNo(object? sender, RoutedEventArgs e)
    {
        _work?.Forget();
        Close();
    }

    private void PlaceBottomRight()
    {
        var screen = Screens.Primary;
        if (screen is null)
            return;
        var area = screen.WorkingArea;
        var width = (int)Math.Ceiling(Bounds.Width * screen.Scaling);
        var height = (int)Math.Ceiling(Bounds.Height * screen.Scaling);
        Position = new PixelPoint(area.Right - width - ScreenMargin, area.Bottom - height - ScreenMargin);
    }
}
