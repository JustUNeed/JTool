using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using JTool.Helpers;
using JTool.Services;
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

        // 启动即为悬浮球态：先关自动尺寸，再显式收成小球
        Loaded += (s, e) =>
        {
            SizeToContent = SizeToContent.Manual;
            ShowBallOnly();
        };
    }

    // ===== 形态切换 =====
    // 三个形态统一在这里设置「可见性 + 窗口尺寸」，避免尺寸与形态不一致导致看不见/收不回。

    private void ShowBallOnly()
    {
        SizeToContent = SizeToContent.Manual;
        MenuPanel.Visibility = Visibility.Collapsed;
        DropPanel.Visibility = Visibility.Collapsed;
        BallPanel.Visibility = Visibility.Visible;
        Width = 32;
        Height = 32;
    }

    private void ShowMenu()
    {
        SizeToContent = SizeToContent.Manual;
        BallPanel.Visibility = Visibility.Collapsed;
        DropPanel.Visibility = Visibility.Collapsed;
        MenuPanel.Visibility = Visibility.Visible;
        Width = _vm.PanelWidth;
        Height = _vm.PanelHeight;
    }

    private void ShowDrop()
    {
        BallPanel.Visibility = Visibility.Collapsed;
        MenuPanel.Visibility = Visibility.Collapsed;
        DropPanel.Visibility = Visibility.Visible;
        // 投放态：固定宽度，高度随槽位内容自适应
        SizeToContent = SizeToContent.Height;
        Width = 130;
    }

    private void Ball_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_suppressHover) return;
        _collapseTimer.Stop();
        ShowMenu();
    }

    private void ApplyVisibility()
    {
        if (_vm.IsBallVisible) { Show(); ShowBallOnly(); }
        else Hide();
    }

    // ===== 文件/数据拖入 =====
    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (!DragDataParser.CanAccept(e.Data)) { e.Effects = DragDropEffects.None; e.Handled = true; return; }
        _collapseTimer.Stop();
        ShowDrop();
        BuildDropSlots(e.Data);
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDataParser.CanAccept(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
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

    private void BuildDropSlots(IDataObject data)
    {
        DropSlotsPanel.Children.Clear();

        bool isFile = data.GetDataPresent(DataFormats.FileDrop);
        bool hasFolder = DragDataParser.HasFolder(data);
        bool hasImageData = data.GetDataPresent(DataFormats.Bitmap);
        string? droppedText = DragDataParser.GetText(data);
        bool isImageUrl = DragDataParser.IsProbablyImageUrl(droppedText);
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
                    d => _ = SaveWebImageSafe(d, captured)));
            }
        }
    }

    private async void AddToBoard(IDataObject data)
    {
        var bmp = DragDataParser.GetBitmap(data);
        if (bmp != null) { _vm.AddImageToBoard(bmp); return; }

        string? text = DragDataParser.GetText(data);
        if (string.IsNullOrWhiteSpace(text)) return;

        // 乐观策略：可能是图片就先当图片下载，失败再退回文本
        if (DragDataParser.IsProbablyImageUrl(text))
        {
            bool ok = await _vm.AddImageFromUrlAsync(text);
            if (!ok) _vm.AddTextToBoard(text);
            return;
        }
        _vm.AddTextToBoard(text);
    }

    // 保存网络/位图图片到目录，集中处理异常提示（Service 不再弹窗）
    private async Task SaveWebImageSafe(IDataObject data, string targetDir)
    {
        try { await _vm.SaveWebImageAsync(data, targetDir); }
        catch (Exception ex)
        {
            MessageBox.Show($"保存图片失败：{ex.Message}", "JTool",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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

    // ===== 右下角对角缩放 =====
    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        _vm.PanelWidth += e.HorizontalChange;   // VM 内已夹取 160~800
        _vm.PanelHeight += e.VerticalChange;    // VM 内已夹取 120~900
        Width = _vm.PanelWidth;
        Height = _vm.PanelHeight;
    }

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _vm.SavePanelSize(_vm.PanelWidth, _vm.PanelHeight);
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
