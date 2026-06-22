using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace JTool.Core;

public enum ToastLevel { Info, Success, Error }



/// <summary>鼠标位置堆叠式 toast。新提示出现在鼠标处，旧的往上堆叠，各自计时淡出后补位。</summary>
public sealed class ToastService
{
    private const int Gap = 6;          // 两条之间的间隙
    private const int CursorOffsetY = 24;   // 与鼠标的竖直偏移，避免压住光标
    private readonly List<ToastWindow> _stack = new();

    public void Info(string msg, int ms = 2000) => Show(msg, ToastLevel.Info, ms);
    public void Success(string msg, int ms = 2000) => Show(msg, ToastLevel.Success, ms);
    public void Error(string msg, int ms = 3000) => Show(msg, ToastLevel.Error, ms);

    public void Show(string message, ToastLevel level, int ms = 2500)
    {
        var app = Application.Current;
        if (app == null) return;
        app.Dispatcher.Invoke(() =>
        {
            // 以"弹出此条时的鼠标位置"作为整列锚点（每次弹新条都更新到当前鼠标处）
            _anchor = NativeMethods.GetCursorScreenPoint();

            var toast = new ToastWindow(message, level, ms, onClosed: OnToastClosed);
            _stack.Add(toast);
            toast.Show();
            Relayout();
        });
    }

    private Point _anchor;

    private void OnToastClosed(ToastWindow t)
    {
        _stack.Remove(t);
        Relayout();
    }

    /// <summary>以锚点（鼠标处）为基准，从下往上依次摆放。最新一条在最下、贴近鼠标。</summary>
    private void Relayout()
    {
        double baseY = _anchor.Y - CursorOffsetY ;   // 最底部一条的底边
        double left = _anchor.X ;               // 鼠标右侧一点

        // _stack 顺序为旧→新；让最新的贴鼠标，旧的往上排，所以从后往前布局
        double y = baseY;
        for (int i = _stack.Count - 1; i >= 0; i--)
        {
            var t = _stack[i];
            double h = t.ActualHeight > 0 ? t.ActualHeight : t.DesiredHeight;
            y -= h;
            t.MoveTo(left, y);
            y -= Gap;
        }
    }
}


internal sealed class ToastWindow : Window
{

    private readonly DispatcherTimer _timer;
    private readonly Action<ToastWindow> _onClosed;

    public double DesiredWidth => Content is FrameworkElement fe ? fe.ActualWidth : 200;
    public double DesiredHeight => Content is FrameworkElement fe ? fe.ActualHeight : 36;

    public ToastWindow(string message, ToastLevel level, int ms, Action<ToastWindow> onClosed)
    {
        _onClosed = onClosed;
        Top = NativeMethods.GetCursorScreenPoint().Y;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;
        IsHitTestVisible = false;       // 点击穿透
        ShowActivated = false;          // 不抢焦点

        var bg = level switch
        {
            ToastLevel.Success => ("#FF388E3C"),
            ToastLevel.Error => ("#FFD32F2F"),
            _ => ("#FF424242")
        };

        Content = new Border
        {
            Background = (Brush)new BrushConverter().ConvertFromString(bg)!,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 7, 12, 7),
            Child = new TextBlock
            {
                Text =  message,
                Foreground = Brushes.White,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 280
            }
        };

        Loaded += (_, _) => FadeIn();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
        _timer.Tick += (_, _) => { _timer.Stop(); FadeOutAndClose(); };
        _timer.Start();
    }

    public void MoveTo(double left, double top)
    {
        // 用动画平滑移动到新位置（下方 toast 消失时上面的滑下来）
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        Left = left;
        var anim = new DoubleAnimation(Top, top, TimeSpan.FromMilliseconds(120));
        BeginAnimation(TopProperty, anim);
    }

    private void FadeIn()
    {
        Opacity = 0;
        BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));
    }

    private void FadeOutAndClose()
    {
        var anim = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(250));
        anim.Completed += (_, _) => CloseNow();
        BeginAnimation(OpacityProperty, anim);
    }

    public void CloseNow()
    {
        _timer.Stop();
        try { Close(); } catch { }
        _onClosed(this);
    }
}
