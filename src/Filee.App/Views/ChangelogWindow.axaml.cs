using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Filee.App.Services;
using Filee.Core.Localization;

namespace Filee.App.Views;

/// <summary>Halodrop change: the notes of one or more versions (<see cref="ChangelogEntry"/>), newest first.</summary>
public partial class ChangelogWindow : Window
{
    private readonly ILocalizer? _loc;
    private readonly IReadOnlyList<ChangelogEntry> _entries = [];
    private readonly Action? _tour;

    public ChangelogWindow() => InitializeComponent();

    /// <param name="tour">Opens the feature tour (the slides); null hides its button.</param>
    public ChangelogWindow(IReadOnlyList<ChangelogEntry> entries, ILocalizer loc, Action? tour) : this()
    {
        _entries = entries;
        _loc = loc;
        _tour = tour;
        UiZoom.Track(this);
        TourButton.IsVisible = tour is not null;
        ShowTexts();
        loc.LanguageChanged += OnLanguageChanged;
        Closed += (_, _) => loc.LanguageChanged -= OnLanguageChanged;
        Header.PointerPressed += OnHeaderPressed;
        Opened += (_, _) => OkButton.Focus();
    }

    /// <summary>The versions shown (tests).</summary>
    internal IReadOnlyList<ChangelogEntry> Entries => _entries;

    /// <summary>The panel holding the notes (tests).</summary>
    internal StackPanel NotesPanel => Notes;

    private void OnLanguageChanged(object? sender, EventArgs e) => ShowTexts();

    private void ShowTexts()
    {
        if (_loc is null)
            return;
        Title = HeaderText.Text = _entries.Count == 1
            ? _loc.Format("whats_new.title", _entries[0].DisplayVersion)
            : _loc["changelog.title"];
        Notes.Children.Clear();
        var english = _loc.Language == "en";
        foreach (var entry in _entries)
        {
            if (Notes.Children.Count > 0)
                Notes.Children.Add(new Border { Height = 1, Margin = new Thickness(0, 10, 0, 6), Background = Brush("SubtleBorderBrush") });
            Notes.Children.Add(new TextBlock
            {
                Text = _loc.Format("changelog.version", entry.DisplayVersion),
                FontWeight = FontWeight.SemiBold,
                FontSize = 15,
                Margin = new Thickness(0, 0, 0, 2),
            });
            // In another language: the version's summaries in that language first, when it has its own.
            var summaries = english ? [] : entry.Note.Summary.TryGetValue(_loc.Language, out var own) ? own : [];
            foreach (var line in summaries)
                Notes.Children.Add(Bullet(line));
            if (summaries.Count > 0 && entry.Blocks.Count > 0)
                Notes.Children.Add(new TextBlock
                {
                    Text = _loc["changelog.details_english"],
                    Foreground = Brush("TextSecondaryBrush"),
                    FontSize = 12,
                    Margin = new Thickness(0, 6, 0, 0),
                });
            foreach (var block in entry.Blocks)
            {
                // The notes are in English: left to right in every language, quieter under the user's own summaries.
                var control = block.Kind switch
                {
                    ChangelogBlockKind.Bullet => Bullet(block.Text, summaries.Count > 0),
                    ChangelogBlockKind.Heading => Paragraph(block.Text, FontWeight.SemiBold, summaries.Count > 0),
                    _ => Paragraph(block.Text, FontWeight.Normal, summaries.Count > 0),
                };
                if (!english)
                    control.FlowDirection = FlowDirection.LeftToRight;
                Notes.Children.Add(control);
            }
        }
    }

    private Control Bullet(string text, bool secondary = false)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("16,*"), Margin = new Thickness(0, 2, 0, 0) };
        grid.Children.Add(new TextBlock { Text = "•", Foreground = Brush("AccentBrush"), VerticalAlignment = VerticalAlignment.Top });
        var body = Rich(text);
        if (secondary)
            body.Foreground = Brush("TextSecondaryBrush");
        Grid.SetColumn(body, 1);
        grid.Children.Add(body);
        return grid;
    }

    private Control Paragraph(string text, FontWeight weight, bool secondary)
    {
        var block = Rich(text);
        block.FontWeight = weight;
        block.Margin = new Thickness(0, 4, 0, 0);
        if (secondary)
            block.Foreground = Brush("TextSecondaryBrush");
        return block;
    }

    /// <summary>A wrapping text block with the **bold** runs drawn bold.</summary>
    private static TextBlock Rich(string text)
    {
        var block = new TextBlock { TextWrapping = TextWrapping.Wrap };
        foreach (var (run, bold) in Changelog.Runs(text))
            block.Inlines!.Add(new Run(run) { FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal });
        return block;
    }

    private IBrush? Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void OnTour(object? sender, RoutedEventArgs e)
    {
        Close();
        _tour?.Invoke();
    }

    private async void OnReleases(object? sender, RoutedEventArgs e) =>
        await Launcher.LaunchUriAsync(new Uri(UpdateService.RepositoryUrl.TrimEnd('/') + "/releases"));

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnHeaderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && e.Source is not Button)
            BeginMoveDrag(e);
    }
}
