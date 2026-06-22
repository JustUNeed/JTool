using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using JTool.Core;
using JTool.DragDrop;

namespace JTool.Hosting;

public partial class FloatWindow : Window
{
    private readonly FloatWindowViewModel _vm;
    private readonly DropRouter _router;
    private readonly DispatcherTimer _collapseTimer;
    private bool _suppressHover;
    private bool _draggingWindow;
    private Point _dragOffset;

    public FloatWindow(FloatWindowViewModel vm, DropRouter router)
    {
        InitializeComponent();
        _vm = vm;
        _router = router;
        DataContext = vm;

        Topmost = _vm.Settings.Topmost;
        ApplyBallSize(_vm.Settings.BallSize);

        _collapseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            if (_draggingWindow) return;
            if (_vm.IsPinned) return;                       // 常驻时不收
            if (!IsMouseReallyOver()) { ShowBallOnly(); _suppressHover = false; }
        };

        MouseLeave += (_, _) =>
        {
            if (_draggingWindow) return;     // 正在拖动窗口时不收回
            if (_vm.IsPinned) return;        // 常驻时不收回
            _collapseTimer.Start();
        };

        MouseEnter += (_, _) => _collapseTimer.Stop();

        DragEnter += Window_DragEnter;
        DragOver += Window_DragOver;
        DragLeave += Window_DragLeave;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FloatWindowViewModel.IsBallVisible))
                ApplyVisibility();

            if (e.PropertyName == nameof(FloatWindowViewModel.IsPinned)
               && !_vm.IsPinned && !IsMouseReallyOver())
                _collapseTimer.Start();                     // 取消常驻且鼠标已离开→收回
        };

        Loaded += (_, _) =>
        {
            SizeToContent = SizeToContent.Manual;
            if (_vm.IsPinned)
                ShowMenu();   // 新增：常驻则直接显示面板
            else ShowBallOnly();
        };
    }

    private void ApplyBallSize(double size)
    {
        BallPanel.Width = size;
        BallPanel.Height = size;
    }

    // ===== 三态切换 =====
    private void ShowBallOnly()
    {
        SizeToContent = SizeToContent.Manual;
        MenuPanel.Visibility = Visibility.Collapsed;
        DropPanel.Visibility = Visibility.Collapsed;
        BallPanel.Visibility = Visibility.Visible;
        Width = BallPanel.Width;
        Height = BallPanel.Height;
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
        SizeToContent = SizeToContent.Height;
        Width = 140;
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

    // ===== 拖入：解析 + 用 router 生成槽 =====
    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (!_router.CanAccept(e.Data)) { e.Effects = DragDropEffects.None; e.Handled = true; return; }

        if (_vm.IsPinned)
        {
            // 常驻：不切投放态，直接把槽填进右侧竖条，鼠标移过去即可落
            BuildDockSlots(_router.Parse(e.Data));
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        _collapseTimer.Stop();
        ShowDrop();
        BuildSlots(_router.Parse(e.Data));
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }





    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = _router.CanAccept(e.Data) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }


    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        if (_vm.IsPinned)
        {
            if (!IsMouseReallyOver()) ClearDockSlots();
            return;
        }
        if (!IsMouseReallyOver()) ShowBallOnly();
    }





    private void BuildSlots(DropContext ctx)
    {
        DropSlotsPanel.Children.Clear();
        foreach (var slot in _router.CollectSlots(ctx))
            DropSlotsPanel.Children.Add(CreateSlotButton(slot, ctx));
    }

    private Border CreateSlotButton(DropSlot slot, DropContext ctx)
    {
        var border = new Border
        {
            Height = 40,
            Margin = new Thickness(2),
            CornerRadius = new CornerRadius(6),
            Background = (Brush)new BrushConverter().ConvertFromString("#FF3F51B5")!,
            AllowDrop = true
        };
        border.Child = new TextBlock
        {
            Text = slot.Title,
            Foreground = Brushes.White,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        border.DragEnter += (_, e) => { border.Opacity = 0.7; e.Effects = DragDropEffects.Copy; e.Handled = true; };
        border.DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
        border.DragLeave += (_, e) => { border.Opacity = 1.0; e.Handled = true; };
        border.Drop += (_, e) =>
        {
            border.Opacity = 1.0;
            try { slot.OnDrop(ctx); }
            catch (Exception ex) { Logger.Error("投放执行失败", ex); }
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
        _vm.WindowLeft = Left;
        _vm.WindowTop = Top;
        _vm.SaveGeometry();

        if (_vm.IsPinned)
        {
            ShowMenu();
        }   // 常驻：保持面板
        else
        { 
            ShowBallOnly(); 
        }   // 非常驻：收起面板
         
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






    // ===== 常驻竖条（DockStrip）=====
    // 拖拽进入面板范围 → 实时填充竖条的槽并高亮；每个槽自己接收 Drop，直接执行不弹菜单。

    private void DockStrip_DragEnter(object sender, DragEventArgs e)
    {
        if (!_router.CanAccept(e.Data)) { e.Effects = DragDropEffects.None; e.Handled = true; return; }
        BuildDockSlots(_router.Parse(e.Data));
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void DockStrip_DragLeave(object sender, DragEventArgs e)
    {
        // 鼠标真正离开竖条范围才清空（避免在子槽间移动时误清）
        var p = NativeMethods.GetCursorScreenPoint();
        var tl = DockStrip.PointToScreen(new Point(0, 0));
        bool inside = p.X >= tl.X && p.X <= tl.X + DockStrip.ActualWidth
                   && p.Y >= tl.Y && p.Y <= tl.Y + DockStrip.ActualHeight;
        if (!inside) ClearDockSlots();
        e.Handled = true;
    }

    private void BuildDockSlots(DropContext ctx)
    {
        DockSlotsPanel.Children.Clear();
        var slots = _router.CollectSlots(ctx);
        DockHint.Visibility = slots.Count > 0 ? Visibility.Collapsed : Visibility.Visible;

        foreach (var slot in slots)
            DockSlotsPanel.Children.Add(CreateDockSlot(slot, ctx));
    }

    private void ClearDockSlots()
    {
        DockSlotsPanel.Children.Clear();
        DockHint.Visibility = Visibility.Visible;
    }

    private Border CreateDockSlot(DropSlot slot, DropContext ctx)
    {
        var border = new Border
        {
            MinHeight = 40,
            Margin = new Thickness(0, 0, 0, 6),
            CornerRadius = new CornerRadius(6),
            Background = (Brush)new BrushConverter().ConvertFromString("#FF3F51B5")!,
            AllowDrop = true
        };
        border.Child = new TextBlock
        {
            Text = slot.Title,
            Foreground = Brushes.White,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(4)
        };
        border.DragEnter += (_, e) => { border.Opacity = 0.65; e.Effects = DragDropEffects.Copy; e.Handled = true; };
        border.DragOver += (_, e) => { e.Effects = DragDropEffects.Copy; e.Handled = true; };
        border.DragLeave += (_, e) => { border.Opacity = 1.0; e.Handled = true; };
        border.Drop += (_, e) =>
        {
            border.Opacity = 1.0;
            try { slot.OnDrop(ctx); }
            catch (Exception ex) { Logger.Error("常驻竖条投放失败", ex); }
            e.Handled = true;
            ClearDockSlots();            // 落完清空，回到占位提示；常驻态保持面板不收回
        };
        return border;
    }





    // ===== 工具 =====
    private bool IsMouseReallyOver()
    {
        var p = NativeMethods.GetCursorScreenPoint();
        var tl = PointToScreen(new Point(0, 0));
        return p.X >= tl.X && p.X <= tl.X + ActualWidth
            && p.Y >= tl.Y && p.Y <= tl.Y + ActualHeight;
    }


    private void PasteWidget_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: IPasteTarget target })
        {
            try { target.PasteFromClipboard(); }
            catch (Exception ex) { Logger.Error("粘贴操作失败", ex); }
        }
    }
}
