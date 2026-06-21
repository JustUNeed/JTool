using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using JTool.Services;
using JTool.ViewModels;
using JTool.Views;
using JTUI.Theming;

namespace JTool;

public partial class App : Application
{
    private TaskbarIcon? _trayIcon;
    private FloatWindowViewModel? _vm;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        JTThemeManager.Initialize(JTTheme.Light);


        _vm = new FloatWindowViewModel(
            new ConfigService(),
            new IconService(),
            new FileOperationService(),
            new WebImageService(),
            new BoardService());

        _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
        _trayIcon.DataContext = _vm;
        // 没有 tray.ico 时用系统图标兜底
        _trayIcon.Icon = System.Drawing.SystemIcons.Application;

        var win = new FloatWindow(_vm);
        win.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
