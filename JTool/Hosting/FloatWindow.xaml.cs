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
            if (!IsMouseReallyOver()) { ShowBallOnly(); _suppressHover = false; }
        };

        MouseLeave += (_, _) => { if (!_draggingWindow) _collapseTimer.Start(); };
        MouseEnter += (_, _) => _collapseTimer.Stop();

        DragEnter += Window_DragEnter;
        DragOver += Window_DragOver;
        DragLeave += Window_DragLeave;

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FloatWindowViewModel.IsBallVisible))
                ApplyVisibility();
        };

        Loaded += (_, _) => { SizeToContent = SizeToContent.Manual; ShowBallOnly(); };
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
        ShowBallOnly();
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

    // ===== 工具 =====
    private bool IsMouseReallyOver()
    {
        var p = NativeMethods.GetCursorScreenPoint();
        var tl = PointToScreen(new Point(0, 0));
        return p.X >= tl.X && p.X <= tl.X + ActualWidth
            && p.Y >= tl.Y && p.Y <= tl.Y + ActualHeight;
    }
}
