using JTool.Core;
using JTool.Settings;
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
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace JTool.Hosting;

public partial class FloatWindow : JTWindow
{
    private readonly FloatWindowViewModel _vm;
    private readonly JsonStore<FileGridData> _fileStore = new("files.json");
    private readonly JsonStore<TextBoardData> _textStore = new("texts.json");
    private readonly JsonStore<FolderBinData> _folderStore = new("folders.json");

    private bool _draggingWindow;
    private Point _dragOffset;
    private FrameworkElement? _dragElement;   // 当前正在用于拖窗的元素（球或拖动块）

    // 缩放：记录拖动起点的窗口尺寸，避免用"当前 Width"做基准导致抽搐
    private double _resizeStartW;
    private double _resizeStartH;

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
        MinWidth = 16;

        Topmost = _vm.Settings.Topmost;
        ApplyBallSize(_vm.Settings.BallSize);

        // 设置项实时同步：球大小 / 总在最前
        _vm.Settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AppSettings.BallSize))
                Dispatcher.Invoke(() => ApplyBallSize(_vm.Settings.BallSize));
            else if (e.PropertyName == nameof(AppSettings.Topmost))
                Dispatcher.Invoke(() => Topmost = _vm.Settings.Topmost);
        };

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

        // 拖入内容时展开面板；窗口级只负责展开，不接管 Drop，
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




        ApplyBallSize(_vm.Settings.BallSize);
        ApplyBallAppearance();

        _vm.Settings.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(AppSettings.BallSize):
                    Dispatcher.Invoke(() => ApplyBallSize(_vm.Settings.BallSize));
                    break;
                case nameof(AppSettings.Topmost):
                    Dispatcher.Invoke(() => Topmost = _vm.Settings.Topmost);
                    break;
                case nameof(AppSettings.BallColor):
                case nameof(AppSettings.BallText):
                case nameof(AppSettings.BallCornerRadius):
                case nameof(AppSettings.BallImagePath):
                    Dispatcher.Invoke(ApplyBallAppearance);
                    break;
            }
        };







    }

    // ===== 唯一的状态切换入口（幂等，避免重复赋值/抖动）=====
    private void SetShape(Shape shape)
    {
        if (_shape == shape) return;
        _shape = shape;
        SizeToContent = SizeToContent.Manual;

  

        if (shape == Shape.Panel)
        {
            PanelRootBackground.Visibility = Visibility.Visible;
            BallPanel.Visibility = Visibility.Collapsed;
            PanelRoot.Visibility = Visibility.Visible;     // 显示面板内容
            Width = _vm.PanelWidth;
            Height = _vm.PanelHeight;
            _hoverTimer.Start();
        }
        else
        {
            PanelRootBackground.Visibility = Visibility.Collapsed;
            PanelRoot.Visibility = Visibility.Collapsed;   // 关键：球态隐藏面板内容
            BallPanel.Visibility = Visibility.Visible;
            Width = BallPanel.Width;
            Height = BallPanel.Height;
            _hoverTimer.Stop();
        }
    }

    // 鼠标移入小图标 → 展开面板（唯一展开入口之一）
    private void Ball_MouseEnter(object sender, MouseEventArgs e) => SetShape(Shape.Panel);

    // 面板态下定时检查：鼠标真的离开窗口矩形、未在拖窗、未常驻，才收回
    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        if (_shape != Shape.Panel) { _hoverTimer.Stop(); return; }
        if (_draggingWindow || _vm.IsPinned) return;   // 常驻不收回
        if (!IsCursorInsideWindow()) SetShape(Shape.Ball);
    }

    private bool IsCursorInsideWindow()
    {
        var p = NativeMethods.GetCursorScreenPoint();   // 屏幕坐标，绕开透明窗 MouseLeave 误报
        return p.X >= Left && p.X <= Left + Width
            && p.Y >= Top && p.Y <= Top + Height;
    }

    // 球大小同时驱动：球、拖动块、顶栏让位列；球态下立即同步窗口尺寸
    private void ApplyBallSize(double size)
    {
        if (size < 16) size = 16;

        BallPanel.Width = size;
        BallPanel.Height = size;
        BallTextBlock.FontSize = size * 0.6;
        DragTextBlock.FontSize = size * 0.6;  
        TopBar.Height = size;
        DragSlot.Width = new GridLength(size);

        if (_shape == Shape.Ball)
        {
            Width = size;
            Height = size;
        }
    }

    private void ApplyBallAppearance()
    {
        var s = _vm.Settings;

        bool useImage = !string.IsNullOrWhiteSpace(s.BallImagePath) && File.Exists(s.BallImagePath);

        BitmapImage? img = null;
        if (useImage)
        {
            try
            {
                img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                img.UriSource = new Uri(s.BallImagePath);
                img.EndInit();
                img.Freeze();
            }
            catch (Exception ex)
            {
                Logger.Error("加载悬浮球图片失败", ex);
                img = null;
                useImage = false;
            }
        }

        // 背景色（失败兜底）
        System.Windows.Media.Brush bg;
        try
        {
            bg = (System.Windows.Media.Brush)
                new System.Windows.Media.BrushConverter().ConvertFromString(s.BallColor)!;
        }
        catch
        {
            bg = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x3F, 0x51, 0xB5));
        }

        // ===== 悬浮球：可图片可纯色 =====
        BallPanel.CornerRadius = new CornerRadius(s.BallCornerRadius);
        if (useImage && img != null)
        {
            BallImage.Source = img;
            BallImage.Visibility = Visibility.Visible;
            BallTextBlock.Visibility = Visibility.Collapsed;
            BallPanel.Background = System.Windows.Media.Brushes.Transparent;
        }
        else
        {
            BallImage.Visibility = Visibility.Collapsed;
            BallImage.Source = null;
            BallPanel.Background = bg;
            BallTextBlock.Text = s.BallText ?? "";
            BallTextBlock.Visibility = string.IsNullOrEmpty(s.BallText)
                ? Visibility.Collapsed : Visibility.Visible;
        }

        // ===== 拖动块：始终纯色 + 文字（不显示图片） =====
        DragHandle.CornerRadius = new CornerRadius(s.BallCornerRadius);
        DragHandle.Background = bg;
        DragTextBlock.Text = s.BallText ?? "";
        DragTextBlock.Visibility = string.IsNullOrEmpty(s.BallText)
            ? Visibility.Collapsed : Visibility.Visible;
    }




    protected override void OnClosed(EventArgs e)
    {
        _hoverTimer.Stop();
        _hoverTimer.Tick -= HoverTimer_Tick;
        _vm.WindowLeft = Left;
        _vm.WindowTop = Top;
        _vm.SaveGeometry();
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

    // ===== 窗口拖动（球态点 BallPanel、面板态点 DragHandle，复用同一逻辑）=====
    private void DragHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragElement = (FrameworkElement)sender;
        _draggingWindow = true;
        _dragOffset = e.GetPosition(this);
        _dragElement.CaptureMouse();
        _dragElement.MouseMove += DragHandle_MouseMove;
        _dragElement.MouseLeftButtonUp += DragHandle_MouseUp;
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
        if (_dragElement != null)
        {
            _dragElement.ReleaseMouseCapture();
            _dragElement.MouseMove -= DragHandle_MouseMove;
            _dragElement.MouseLeftButtonUp -= DragHandle_MouseUp;
            _dragElement = null;
        }

        // 回写位置并持久化
        _vm.WindowLeft = Left;
        _vm.WindowTop = Top;
        _vm.SaveGeometry();
        e.Handled = true;
    }

    // ===== 缩放（用拖动起点尺寸 + 累计增量，杜绝抽搐）=====
    private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        // 记录起点：窗口左上角屏幕坐标固定不动，缩放只改宽高
        _resizeStartW = Width;
        _resizeStartH = Height;
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_shape != Shape.Panel) return;
        var p = NativeMethods.GetCursorScreenPoint();
        _vm.PanelWidth = p.X - Left;     // setter 内部已 Clamp
        _vm.PanelHeight = p.Y - Top;
        Width = _vm.PanelWidth;
        Height = _vm.PanelHeight;
    }

    private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        => _vm.SaveGeometry();

    
}
