using JTool.Core;
using JTool.Widgets.TextBoard;
using JTUI.Controls;
using JTUI.Controls.FolderBin;
using JTUI.Controls.Viewer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace JTool.Hosting;

public partial class FloatWindow : JTWindow
{
    private readonly FloatWindowViewModel _vm;
    private readonly JsonStore<FileGridData> _fileStore = new("files.json");
    private readonly JsonStore<TextBoardData> _textStore = new("texts.json");
    private readonly JsonStore<FolderBinData> _folderStore = new("folders.json");

    private bool _draggingWindow;
    private Point _dragOffset;

    // ===== 单一状态来源 =====
    private enum Shape { Ball, Panel }
    private Shape _shape = Shape.Ball;

    // 收回判定：不信任 WPF 透明窗的 MouseLeave（子控件间移动会误报），用屏幕坐标兜底
    private readonly DispatcherTimer _hoverTimer =
        new() { Interval = TimeSpan.FromMilliseconds(120) };

    public FloatWindow(FloatWindowViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;

        // 关键：彻底关掉内容撑窗，宽高完全由代码控制
        SizeToContent = SizeToContent.Manual;
        MaxWidth = 800;          // 和 PanelWidth 上限一致，双保险
        MinWidth = 32;


        Topmost = _vm.Settings.Topmost;
        ApplyBallSize(_vm.Settings.BallSize);

        _hoverTimer.Tick += HoverTimer_Tick;

        // 启动时按持久化的常驻状态决定初始形态
        Loaded += (_, _) => SetShape(_vm.IsPinned ? Shape.Panel : Shape.Ball);

        // 取消常驻、且鼠标已不在窗口内 → 立即收回
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FloatWindowViewModel.IsPinned)
                && !_vm.IsPinned && !IsCursorInsideWindow())
                SetShape(Shape.Ball);
        };

        // 需求2：拖入内容时展开面板；窗口级只负责展开，不接管 Drop，
        // 松手落点由各 JTUI 子控件（AllowDropImport）自行处理。
        DragEnter += (_, e) =>
        {
            SetShape(Shape.Panel);
            // 不设置 e.Handled，让事件继续冒泡给子控件，由它们决定 Effects
        };

        InitFileGrid();
        InitImageGrid();
        InitTextList();
        InitFolderBin();
    }

    // ===== 唯一的状态切换入口（幂等，避免重复赋值/抖动）=====
    private void SetShape(Shape shape)
    {
        if (_shape == shape) return;
        _shape = shape;
        SizeToContent = SizeToContent.Manual;

        if (shape == Shape.Panel)
        {
            BallPanel.Visibility = Visibility.Collapsed;
            PanelRoot.Visibility = Visibility.Visible;     // 显示面板内容
            Width = _vm.PanelWidth;
            Height = _vm.PanelHeight;
            _hoverTimer.Start();
        }
        else
        {
            PanelRoot.Visibility = Visibility.Collapsed;   // 关键：球态隐藏面板内容
            BallPanel.Visibility = Visibility.Visible;
            Width = BallPanel.Width;
            Height = BallPanel.Height;
            _hoverTimer.Stop();
        }
    }


    // 需求1：鼠标移入小图标 → 展开面板（唯一展开入口之一）
    private void Ball_MouseEnter(object sender, MouseEventArgs e) => SetShape(Shape.Panel);

    // 面板态下定时检查：鼠标真的离开窗口矩形、未在拖窗、未常驻，才收回
    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        if (_shape != Shape.Panel) { _hoverTimer.Stop(); return; }
        if (_draggingWindow || _vm.IsPinned) return;   // 需求3：常驻不收回
        if (!IsCursorInsideWindow()) SetShape(Shape.Ball);
    }

    private bool IsCursorInsideWindow()
    {
        var p = NativeMethods.GetCursorScreenPoint();   // 屏幕坐标，绕开透明窗 MouseLeave 误报
        return p.X >= Left && p.X <= Left + Width
            && p.Y >= Top && p.Y <= Top + Height;
    }

    private void ApplyBallSize(double size)
    {
        BallPanel.Width = size;
        BallPanel.Height = size;
    }

    protected override void OnClosed(EventArgs e)
    {
        _hoverTimer.Stop();
        _hoverTimer.Tick -= HoverTimer_Tick;
        base.OnClosed(e);
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

    // ===== JTImageGrid 接管 =====
    private void InitImageGrid()
    {
        ImageGrid.ImageDirectory = Paths.BoardImagesDir;

        ImageGrid.ImageLeftClick += path =>
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

        ImageGrid.ImageRightClick += path =>
        {
            var win = new JTWindow { Width = 1000, Height = 700, Title = "预览", TitleBarMode = JTTitleBarMode.Immersive };
            win.Content = new JTImageViewer { ImagePath = path };
            win.Show();
        };

        ImageGrid.ImageDeleted += path =>
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { Logger.Error($"删除图片失败: {path}", ex); }
        };
    }

    // ===== JTFolderBin 接管 =====
    private void InitFolderBin()
    {
        var data = _folderStore.Load();
        Bin.SetFolders(data.Paths);

        Bin.ListChanged += paths =>
            _folderStore.Save(new FolderBinData { Paths = new List<string>(paths) });

        Bin.ItemClicked += path =>
        {
            try { Process.Start("explorer.exe", path); }
            catch (Exception ex) { Logger.Error($"打开文件夹失败: {path}", ex); }
        };

        Bin.ItemRightClick += path => ShowFolderMenu(path);

        Bin.Dropped += r =>
        {
            if (r.Kind == JTFolderDropKind.Failed)
                Logger.Error($"投放失败: {r.FolderPath} :: {r.Error}");
            else
                Logger.Info($"已放入 {r.FolderPath}: {r.ResultPath}");
        };
    }

    private void ShowFolderMenu(string path)
    {
        // TODO: 按需要弹出右键菜单（在资源管理器中打开/移除/复制路径等）
    }

    // ===== JTTextList 接管 =====
    private void InitTextList()
    {
        var data = _textStore.Load();
        TextList.SetItems(data.Items.Select(it => it.Text));

        TextList.ListChanged += texts =>
        {
            var d = new TextBoardData
            {
                Items = texts.Select(t => new TextBoardItem
                {
                    Text = t,
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                }).ToList()
            };
            _textStore.Save(d);
        };

        TextList.ItemClicked += text =>
        {
            try { Clipboard.SetText(text); }
            catch (Exception ex) { Logger.Error("复制文本失败", ex); }
        };

        TextList.ItemRightClick += text => ShowTextMenu(text);
    }

    private void ShowTextMenu(string text)
    {
        // TODO: 按需要弹出右键菜单（删除/编辑/复制等）
    }

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
