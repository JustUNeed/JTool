using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using JTool.Core;
using JTUI.Controls;
using JTUI.Controls.Viewer;

namespace JTool.Hosting;

public partial class FloatWindow : Window
{
    private readonly FloatWindowViewModel _vm;
    private readonly JsonStore<FileGridData> _fileStore = new("files.json");
    private bool _draggingWindow;
    private Point _dragOffset;

    public FloatWindow(FloatWindowViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        Topmost = _vm.Settings.Topmost;
        ApplyBallSize(_vm.Settings.BallSize);

        // 鼠标离开 → 收回成球（常驻时不收）
        MouseLeave += (_, _) =>
        {
            if (_draggingWindow) return;
            if (_vm.IsPinned) return;          // 常驻：不收回
            ShowBallOnly();
        };

        // 拖入数据 → 弹出快捷面板形态
        DragEnter += (_, e) =>
        {
            ShowMenu();
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        };
        DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };

        // 取消常驻且鼠标已离开 → 收回
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FloatWindowViewModel.IsPinned)
                && !_vm.IsPinned && !IsMouseOver)
                ShowBallOnly();
        };

        Loaded += (_, _) =>
        {
            SizeToContent = SizeToContent.Manual;
            if (_vm.IsPinned) ShowMenu();      // 常驻：启动即展开
            else ShowBallOnly();
        };

        InitFileGrid();
        InitImageGrid();
    }


    // ===== JTFileGrid 接管（含旧 shortcuts.json 迁移）=====
    private void InitFileGrid()
    {
        var data = _fileStore.Load();

        if (data.Paths.Count == 0)
        {
            var migrated = TryMigrateLegacyShortcuts();
            if (migrated.Count > 0)
            {
                data.Paths = migrated;
                _fileStore.Save(data);
            }
        }

        Files.SetItems(data.Paths);

        Files.ItemClicked += path =>
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex) { Logger.Error($"启动失败: {path}", ex); }
        };

        Files.ListChanged += paths =>
            _fileStore.Save(new FileGridData { Paths = new List<string>(paths) });
    }

    private static List<string> TryMigrateLegacyShortcuts()
    {
        try
        {
            var legacyPath = Paths.File("shortcuts.json");
            if (!File.Exists(legacyPath)) return new();

            var json = File.ReadAllText(legacyPath);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var result = new List<string>();
            if (doc.RootElement.TryGetProperty("Items", out var items)
                && items.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var it in items.EnumerateArray())
                    if (it.TryGetProperty("Path", out var p) && p.GetString() is { Length: > 0 } s)
                        result.Add(s);
            }
            return result;
        }
        catch (Exception ex) { Logger.Error("迁移旧 shortcuts.json 失败", ex); return new(); }
    }

    // ===== JTImageGrid 接管（路径指向原图片看板目录）=====
    private void InitImageGrid()
    {
        Images.ImageDirectory = Paths.BoardImagesDir;

        Images.ImageLeftClick += path =>
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path);
                bmp.EndInit();
                bmp.Freeze();
                Clipboard.SetImage(bmp);
            }
            catch (Exception ex) { Logger.Error("复制图片失败", ex); }
        };

        Images.ImageRightClick += path =>
        {
            var win = new JTWindow { Width = 1000, Height = 700, Title = "预览" };
            win.Content = new JTImageViewer { ImagePath = path };
            win.Show();
        };

        Images.ImageDeleted += path =>
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { Logger.Error($"删除图片失败: {path}", ex); }
        };
    }

    private void ApplyBallSize(double size)
    {
        BallPanel.Width = size;
        BallPanel.Height = size;
    }

    // ===== 两态切换 =====
    private void ShowBallOnly()
    {
        SizeToContent = SizeToContent.Manual;
        MenuPanel.Visibility = Visibility.Collapsed;
        BallPanel.Visibility = Visibility.Visible;
        Width = BallPanel.Width;
        Height = BallPanel.Height;
    }

    private void ShowMenu()
    {
        SizeToContent = SizeToContent.Manual;
        BallPanel.Visibility = Visibility.Collapsed;
        MenuPanel.Visibility = Visibility.Visible;
        Width = _vm.PanelWidth;
        Height = _vm.PanelHeight;
    }

    private void Ball_MouseEnter(object sender, MouseEventArgs e) => ShowMenu();

    // ===== 窗口拖动 =====
    private void DragHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _draggingWindow = true;
        _dragOffset = e.GetPosition(this);
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
        _vm.WindowLeft = Left;
        _vm.WindowTop = Top;
        _vm.SaveGeometry();
    }

    // ===== 缩放 =====
    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        _vm.PanelWidth += e.HorizontalChange;
        _vm.PanelHeight += e.VerticalChange;
        Width = _vm.PanelWidth;
        Height = _vm.PanelHeight;
    }

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        => _vm.SaveGeometry();
}

// 新的文件网格持久化结构（纯路径列表）
public sealed class FileGridData
{
    public List<string> Paths { get; set; } = new();
}
