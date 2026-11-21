// Morph change: the card a file action leaves behind (FileActions), and the window whose clipboard it uses: Avalonia
// writes the clipboard through a window, and this one is on screen anyway.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

using Filee.App.Services;

namespace Filee.App.Views;

public partial class QuickNoticeWindow : Window
{
    private const int ScreenMargin = 12;

    public QuickNoticeWindow() : this("", null, false)
    {
    }

    public QuickNoticeWindow(string title, string? detail, bool problem)
    {
        InitializeComponent();
        Motion.Track(this);
        TitleText.Text = title;
        if (problem)
            TitleText.Classes.Add("danger");
        if (!string.IsNullOrWhiteSpace(detail))
        {
            DetailText.Text = detail;
            DetailText.IsVisible = true;
        }
        SizeChanged += (_, _) => PlaceBottomRight();
        PointerPressed += (_, _) => Close();
        // Long enough to read; a problem stays a little longer.
        DispatcherTimer.RunOnce(Close, TimeSpan.FromSeconds(problem ? 7 : 3.5));
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
