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
    private readonly DispatcherTimer _closer;

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
        _closer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(problem ? 7 : 3.5) };
        _closer.Tick += (_, _) =>
        {
            _closer.Stop();
            Close();
        };
        _closer.Start();
    }

    /// <summary>Shows <paramref name="colours"/> (#RRGGBB) as swatches with their codes, and stays a little longer.</summary>
    public void ShowSwatches(IReadOnlyList<string> colours)
    {
        foreach (var hex in colours)
        {
            var swatch = new StackPanel { Margin = new Thickness(0, 0, 8, 6), Spacing = 2 };
            swatch.Children.Add(new Border
            {
                Width = 40,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(hex)),
                BorderBrush = Avalonia.Media.Brushes.Gray,
                BorderThickness = new Thickness(0.5),
            });
            swatch.Children.Add(new TextBlock { Text = hex, FontSize = 10, FlowDirection = Avalonia.Media.FlowDirection.LeftToRight });
            Swatches.Children.Add(swatch);
        }
        Swatches.IsVisible = colours.Count > 0;
        // Colours take a moment to look at.
        _closer.Stop();
        _closer.Interval = TimeSpan.FromSeconds(10);
        _closer.Start();
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
