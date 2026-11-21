// Morph change: the shelf (see ShelfWindow.axaml). Files are only referenced: dragging one out hands the original to
// where it's dropped (the target decides to copy or move it, as from a file manager); removing it from the shelf or
// closing the shelf changes nothing on disk.

using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Filee.App.Services;

namespace Filee.App.Views;

/// <summary>A file on the shelf.</summary>
public sealed class ShelfItem(string path) : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public string Path { get; } = path;
    public string Name { get; } = System.IO.Path.GetFileName(path.TrimEnd('\\', '/')) is { Length: > 0 } name ? name : path;
    public string Folder { get; } = System.IO.Path.GetDirectoryName(path.TrimEnd('\\', '/')) ?? "";

    private Bitmap? _thumbnail;
    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }
}

public partial class ShelfWindow : Window
{
    /// <summary>The most files the shelf holds; the oldest go first.</summary>
    public const int MaxItems = 200;

    private static readonly HashSet<string> Pictures = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp",
    };

    private readonly ObservableCollection<ShelfItem> _items = [];
    private Point? _pressedAt;
    private PointerPressedEventArgs? _press;
    private ShelfItem? _pressedItem;
    private bool _placed;

    public ShelfWindow()
    {
        InitializeComponent();
        Motion.Track(this);
        Items.ItemsSource = _items;
        _items.CollectionChanged += (_, _) => Refresh();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        Header.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        };
        DragAll.PointerPressed += async (_, e) => await DragOutAsync(e, [.. _items]);
        Items.AddHandler(PointerPressedEvent, OnItemPressed, RoutingStrategies.Tunnel);
        Items.AddHandler(PointerMovedEvent, OnItemMoved, RoutingStrategies.Tunnel);
        Items.AddHandler(PointerReleasedEvent, (_, _) => (_press, _pressedItem) = (null, null), RoutingStrategies.Tunnel);
        Refresh();
    }

    public IReadOnlyList<string> Paths => [.. _items.Select(i => i.Path)];

    /// <summary>Puts <paramref name="paths"/> on the shelf (once each) and shows it at the right edge of the screen.</summary>
    public void Add(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(p => File.Exists(p) || Directory.Exists(p)))
        {
            if (_items.Any(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase)))
                continue;
            var item = new ShelfItem(path);
            _items.Add(item);
            if (Pictures.Contains(System.IO.Path.GetExtension(path)))
                _ = LoadThumbnailAsync(item);
        }
        while (_items.Count > MaxItems)
            _items.RemoveAt(0);
        if (!IsVisible)
            Show();
        if (!_placed)
            PlaceAtRightEdge();
    }

    private static async Task LoadThumbnailAsync(ShelfItem item)
    {
        try
        {
            var bitmap = await Task.Run(() =>
            {
                using var stream = File.OpenRead(item.Path);
                return Bitmap.DecodeToWidth(stream, 72);
            });
            await Dispatcher.UIThread.InvokeAsync(() => item.Thumbnail = bitmap);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException
                                       or NotSupportedException)
        {
            // No picture: the file icon stays.
        }
    }

    private void Refresh()
    {
        CountText.Text = _items.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);
        EmptyText.IsVisible = _items.Count == 0;
        DragAll.IsVisible = _items.Count > 0;
    }

    private void PlaceAtRightEdge()
    {
        if (Screens.Primary is not { } screen)
            return;
        _placed = true;
        var area = screen.WorkingArea;
        var width = (int)Math.Ceiling(Width * screen.Scaling);
        Position = new PixelPoint(area.Right - width - 16, area.Y + area.Height / 4);
    }

    // ---- dropping files on the shelf ----

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DraggedFiles.Offered(e.DataTransfer) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        // Files dragged out of the shelf and back onto it are already here.
        var files = DraggedFiles.Read(e.DataTransfer, forDrop: true).Files;
        if (files.Count > 0)
            Add(files);
        e.Handled = true;
    }

    // ---- dragging files out ----

    private void OnItemPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || (e.Source as Control)?.FindAncestorOfType<Button>() is not null)
            return;
        _pressedItem = (e.Source as Control)?.DataContext as ShelfItem;
        _pressedAt = e.GetPosition(this);
        _press = e;
    }

    private async void OnItemMoved(object? sender, PointerEventArgs e)
    {
        if (_press is not { } press || _pressedItem is not { } item || _pressedAt is not { } at)
            return;
        var now = e.GetPosition(this);
        if (Math.Abs(now.X - at.X) < 4 && Math.Abs(now.Y - at.Y) < 4)
            return;
        _press = null;
        _pressedItem = null;
        await DragOutAsync(press, [item]);
    }

    private async Task DragOutAsync(PointerPressedEventArgs press, IReadOnlyList<ShelfItem> items)
    {
        if (items.Count == 0)
            return;
        var data = new DataTransfer();
        foreach (var item in items)
        {
            Avalonia.Platform.Storage.IStorageItem? storage = Directory.Exists(item.Path)
                ? await StorageProvider.TryGetFolderFromPathAsync(new Uri(Path.GetFullPath(item.Path)))
                : await StorageProvider.TryGetFileFromPathAsync(new Uri(Path.GetFullPath(item.Path)));
            if (storage is not null)
                data.Add(DataTransferItem.CreateFile(storage));
        }
        if (data.Items.Count > 0)
            await DragDrop.DoDragDropAsync(press, data, DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
    }

    // ---- buttons ----

    private void OnRemove(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is ShelfItem item)
            _items.Remove(item);
    }

    private void OnConvert(object? sender, RoutedEventArgs e)
    {
        var files = Paths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (files.Count == 0)
            return;
        var center = PixelPoint.FromPoint(Bounds.Center, RenderScaling) + Position;
        AppHost.Get<RadialController>().ShowForFiles(center.X - 260, center.Y, files);
    }

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        _items.Clear();
        Hide();
    }
}
