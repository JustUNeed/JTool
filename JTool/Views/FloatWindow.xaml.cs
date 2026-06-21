
using System.IO;

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using JTool.Helpers;
using JTool.ViewModels;

namespace JTool.Views;

public partial class FloatWindow : Window
{
    private readonly FloatWindowViewModel _vm;
    private readonly DispatcherTimer _collapseTimer;
    private bool _suppressHover;
    private bool _draggingWindow;
    private Point _dragOffset;

    public FloatWindow(FloatWindowViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;

        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _collapseTimer.Tick += (s, e) =>
        {
            _collapseTimer.Stop();
            if (_draggingWindow) return;
            if (_vm.IsReordering) return;
            if (!IsMouseReallyOver()) { ShowBallOnly(); _suppressHover = false; }
        };

        MouseLeave += (s, e) =>
        {
            if (_draggingWindow) return;
            if (_vm.IsReordering) return;
            _collapseTimer.Start();
        };
        MouseEnter += (s, e) => _collapseTimer.Stop();

        DragEnter += Window_DragEnter;
        DragOver += Window_DragOver;
        DragLeave += Window_DragLeave;

        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(FloatWindowViewModel.IsBallVisible))
                ApplyVisibility();
            if (e.PropertyName == nameof(FloatWindowViewModel.IsReordering) && !_vm.IsReordering)
                if (!IsMouseReallyOver()) _collapseTimer.Start();
        };

        PreviewMouseLeftButtonUp += (s, e) =>
        {
            if (_vm.IsReordering)
                Dispatcher.BeginInvoke(() =>
                {
                    _vm.ResetReordering();
                    if (!IsMouseReallyOver()) _collapseTimer.Start();
                }, DispatcherPriority.Background);
        };
    }

    // ===== 形态切换 =====
    private void ShowBallOnly()
    {
        MenuPanel.Visibility = Visibility.Collapsed;
        DropPanel.Visibility = Visibility.Collapsed;
        BallPanel.Visibility = Visibility.Visible;
    }

    private void Ball_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_suppressHover) return;
        _collapseTimer.Stop();
        BallPanel.Visibility = Visibility.Collapsed;
        DropPanel.Visibility = Visibility.Collapsed;
        MenuPanel.Visibility = Visibility.Visible;
    }

    private void ApplyVisibility()
    {
        if (_vm.IsBallVisible) { Show(); ShowBallOnly(); }
        else Hide();
    }

    // ===== 文件/数据拖入 =====
    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (!CanAccept(e.Data)) { e.Effects = DragDropEffects.None; e.Handled = true; return; }
        _collapseTimer.Stop();
        BallPanel.Visibility = Visibility.Collapsed;
        MenuPanel.Visibility = Visibility.Collapsed;
        DropPanel.Visibility = Visibility.Visible;
        BuildDropSlots(e.Data);
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = CanAccept(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        var p = NativeMethods.GetCursorScreenPoint();
        var tl = PointToScreen(new Point(0, 0));
        bool inside = p.X >= tl.X && p.X <= tl.X + ActualWidth
                   && p.Y >= tl.Y && p.Y <= tl.Y + ActualHeight;
        if (!inside) ShowBallOnly();
    }

    private static bool CanAccept(IDataObject data)
        => data.GetDataPresent(DataFormats.FileDrop)
        || data.GetDataPresent(DataFormats.Text)
        || data.GetDataPresent(DataFormats.Html)
        || data.GetDataPresent("text/x-moz-url")
        || data.GetDataPresent(DataFormats.Bitmap);

    private void BuildDropSlots(IDataObject data)
    {
        DropSlotsPanel.Children.Clear();

        bool isFile = data.GetDataPresent(DataFormats.FileDrop);
        bool hasFolder = isFile && ((string[])data.GetData(DataFormats.FileDrop)!).Any(Directory.Exists);
        bool hasImageData = data.GetDataPresent(DataFormats.Bitmap);
        string? droppedText = ExtractDroppedText(data);
        bool isImageUrl = droppedText != null && LooksLikeImageUrl(droppedText);
        bool hasText = !string.IsNullOrWhiteSpace(droppedText);
        bool canSaveImage = hasImageData || isImageUrl;

        if (isFile)
        {
            DropSlotsPanel.Children.Add(CreateSlot("＋ 添加快捷方式", "#FF4CAF50",
                files => _vm.AddShortcuts(files)));
            if (hasFolder)
                DropSlotsPanel.Children.Add(CreateSlot("📁 添加为目录", "#FFFF9800",
                    files => _vm.AddTargetDirs(files)));
        }

        if (hasText || hasImageData)
        {
            DropSlotsPanel.Children.Add(CreateSlot("📌 添加到看板", "#FF9C27B0",
                null, d => AddToBoard(d)));
        }

        if (isFile || canSaveImage)
        {
            foreach (var dir in _vm.TargetDirs)
            {
                string captured = dir;
                DropSlotsPanel.Children.Add(CreateSlot("→ " + SafeName(dir), "#FF3F51B5",
                    files => _vm.MoveToDir(files, captured),
                    d => _ = _vm.SaveWebImageAsync(d, captured)));
            }
        }
    }

    private async void AddToBoard(IDataObject data)
    {
        if (data.GetDataPresent(DataFormats.Bitmap)
            && data.GetData(DataFormats.Bitmap) is BitmapSource bmp)
        {
            _vm.AddImageToBoard(bmp);
            return;
        }

        string? text = ExtractDroppedText(data);
        if (string.IsNullOrWhiteSpace(text)) return;

        if (LooksLikeImageUrl(text))
        {
            bool ok = await _vm.AddImageFromUrlAsync(text);
            if (ok) return;
        }
        _vm.AddTextToBoard(text);
    }

    private static string? ExtractDroppedText(IDataObject data)
    {
        if (data.GetDataPresent("text/x-moz-url"))
        {
            var raw = data.GetData("text/x-moz-url") as string;
            var first = raw?.Split('\n').FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(first)) return first;
        }
        if (data.GetDataPresent(DataFormats.Html))
        {
            var html = data.GetData(DataFormats.Html) as string;
            var m = Regex.Match(html ?? "", @"<img[^>]+src=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value;
        }
        if (data.GetDataPresent(DataFormats.Text))
            return (data.GetData(DataFormats.Text) as string)?.Trim();
        return null;
    }

    private static bool LooksLikeImageUrl(string s)
    {
        if (!s.StartsWith("http://") && !s.StartsWith("https://")) return false;
        string lower = s.ToLowerInvariant();
        return lower.Contains(".jpg") || lower.Contains(".jpeg") || lower.Contains(".png")
            || lower.Contains(".gif") || lower.Contains(".webp") || lower.Contains(".bmp")
            || lower.Contains("image") || lower.Contains("/img");
    }

    private Border CreateSlot(string text, string colorHex,
                              Action<string[]>? onFileDrop, Action<IDataObject>? onDataDrop = null)
    {
        var border = new Border
        {
            Height = 44,
            Margin = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Background = (Brush)new BrushConverter().ConvertFromString(colorHex)!,
            AllowDrop = true
        };
        border.Child = new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        border.DragEnter += (s, e) => { border.Opacity = 0.7; e.Effects = DragDropEffects.Copy; e.Handled = true; };
        border.DragOver += (s, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
        border.DragLeave += (s, e) => { border.Opacity = 1.0; e.Handled = true; };
        border.Drop += (s, e) =>
        {
            border.Opacity = 1.0;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                onFileDrop?.Invoke((string[])e.Data.GetData(DataFormats.FileDrop)!);
            else
                onDataDrop?.Invoke(e.Data);
            e.Handled = true;
            ShowBallOnly();
        };
        return border;
    }

    // ===== 窗口拖动 =====
    private void DragHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingWindow = true; _suppressHover = true;
        _dragOffset = e.GetPosition(this);
        _collapseTimer.Stop();
        DragHandle.CaptureMouse();
        DragHandle.MouseMove += DragHandle_MouseMove;
        DragHandle.MouseLeftButtonUp += DragHandle_MouseUp;
        e.Handled = true;
    }

    private void DragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_draggingWindow) return;
        var screen = PointToScreen(e.GetPosition(this));
        Left = screen.X - _dragOffset.X;
        Top = screen.Y - _dragOffset.Y;
    }

    private void DragHandle_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _draggingWindow = false;
        DragHandle.ReleaseMouseCapture();
        DragHandle.MouseMove -= DragHandle_MouseMove;
        DragHandle.MouseLeftButtonUp -= DragHandle_MouseUp;
        _vm.SaveWindowPosition(Left, Top);
        ShowBallOnly();
    }

    // ===== 工具 =====
    private bool IsMouseReallyOver()
    {
        var p = NativeMethods.GetCursorScreenPoint();
        var tl = PointToScreen(new Point(0, 0));
        return p.X >= tl.X && p.X <= tl.X + ActualWidth
            && p.Y >= tl.Y && p.Y <= tl.Y + ActualHeight;
    }

    private static string SafeName(string dir)
    {
        try { return new DirectoryInfo(dir).Name; } catch { return dir; }
    }
}
