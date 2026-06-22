using Hardcodet.Wpf.TaskbarNotification;
using JTool.Core;
using JTool.DragDrop;
using JTool.Hosting;
using JTool.Services;
using JTool.Settings;
using JTool.Widgets.ImageBoard;
using JTool.Widgets.ShortcutGrid;
using JTool.Widgets.TextBoard;
using JTUI.Theming;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace JTool;

public partial class App : Application
{
    private ServiceProvider _provider = null!;
    private TaskbarIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _provider = BuildServices();

        var vm = _provider.GetRequiredService<FloatWindowViewModel>();

        _tray = (TaskbarIcon)FindResource("TrayIcon");
        _tray.DataContext = vm;
        _tray.Icon = System.Drawing.SystemIcons.Application;

        var win = _provider.GetRequiredService<FloatWindow>();
        win.Show();

        JTThemeManager.Initialize(JTTheme.Light);



    }

    private static ServiceProvider BuildServices()
    {
        var s = new ServiceCollection();

        // 基础服务
        s.AddSingleton<SettingsService>();
        s.AddSingleton<AppSettings>(sp => sp.GetRequiredService<SettingsService>().Current);
        s.AddSingleton<IconService>();
        s.AddSingleton<WebImageService>();
        s.AddSingleton<FileMoveService>();
        s.AddSingleton<TargetDirStore>();
        s.AddSingleton<ToastService>();


        // 三个模块 VM（单例：既给面板用，又当投放槽 provider）
        s.AddSingleton<ShortcutGridViewModel>();
        s.AddSingleton<ImageBoardViewModel>();
        s.AddSingleton<TextBoardViewModel>();

        // 投放槽 provider
        s.AddSingleton<IDropSlotProvider>(sp => sp.GetRequiredService<ShortcutGridViewModel>());
        s.AddSingleton<IDropSlotProvider>(sp => sp.GetRequiredService<ImageBoardViewModel>());
        s.AddSingleton<IDropSlotProvider>(sp => sp.GetRequiredService<TextBoardViewModel>());
        s.AddSingleton<IDropSlotProvider, TargetDirSlotProvider>();

        // 拖拽路由
        s.AddSingleton<DropRouter>();

        // 设置窗口
        s.AddTransient<SettingsViewModel>();
        s.AddTransient<SettingsWindow>();

        // 宿主
        s.AddSingleton<FloatWindowViewModel>();
        s.AddSingleton<FloatWindow>();


        return s.BuildServiceProvider();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _provider?.Dispose();
        base.OnExit(e);
    }
}
