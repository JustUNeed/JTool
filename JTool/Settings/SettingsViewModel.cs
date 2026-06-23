using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JTool.Core;
using JTool.Hosting;
using System.Diagnostics;

namespace JTool.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _service;
    private readonly AppSettings _s;

    public SettingsViewModel(SettingsService service)
    {
        _service = service;
        _s = service.Current;
    }

    public bool AutoStart
    {
        get => _s.AutoStart;
        set { if (_s.AutoStart != value) { _s.AutoStart = value; OnPropertyChanged(); } }
    }

    public bool Topmost
    {
        get => _s.Topmost;
        set { if (_s.Topmost != value) { _s.Topmost = value; OnPropertyChanged(); } }
    }

    public double BallSize
    {
        get => _s.BallSize;
        set { if (_s.BallSize != value) { _s.BallSize = value; OnPropertyChanged(); } }
    }

    public bool EnableImageDownload
    {
        get => _s.EnableImageDownload;
        set { if (_s.EnableImageDownload != value) { _s.EnableImageDownload = value; OnPropertyChanged(); } }
    }

    [RelayCommand]
    private void Save() => _service.Save();


    // 打开配置文件夹（%AppData%\JTool）
    [RelayCommand]
    private void OpenConfigFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Paths.Root,
                UseShellExecute = true
            });
        }
        catch (Exception ex) { Logger.Error("打开配置目录失败", ex); }
    }

    // 重置窗口设置（位置/尺寸回默认，常驻关闭）
    [RelayCommand]
    private void ResetWindow()
    {
        try
        {
            var store = new JsonStore<WindowState>("window.json");
            store.Save(new WindowState());   // WindowState 的字段默认值即默认布局
            Logger.Info("窗口设置已重置，重启后生效");
            // 可选：弹个 Toast 提示“重启后生效”
        }
        catch (Exception ex) { Logger.Error("重置窗口设置失败", ex); }
    }
}
