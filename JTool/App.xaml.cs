using Hardcodet.Wpf.TaskbarNotification;
using JTool.Core;

using JTool.Hosting;

using JTool.Settings;



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

        JTThemeManager.Initialize(JTTheme.Dark);



    }

    private static ServiceProvider BuildServices()
    {
        var s = new ServiceCollection();

        // 基础服务
        s.AddSingleton<SettingsService>();
        s.AddSingleton<AppSettings>(sp => sp.GetRequiredService<SettingsService>().Current);




 
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
