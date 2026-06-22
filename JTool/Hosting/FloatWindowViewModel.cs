using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JTool.Core;
using JTool.Settings;

namespace JTool.Hosting;

public sealed partial class FloatWindowViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly SettingsService _settings;
    private readonly JsonStore<WindowState> _winStore = new("window.json");
    private readonly WindowState _win;

    public ObservableCollection<IPanelWidget> Widgets { get; } = new();

    public FloatWindowViewModel(IEnumerable<IPanelWidget> widgets,
        SettingsService settings, IServiceProvider services)
    {
        _services = services;
        _settings = settings;
        _win = _winStore.Load();
        foreach (var w in widgets) Widgets.Add(w);
    }

    public AppSettings Settings => _settings.Current;

    // ===== 窗口几何（持久化）=====
    public double WindowLeft
    {
        get => _win.Left;
        set { if (_win.Left != value) { _win.Left = value; OnPropertyChanged(); } }
    }
    public double WindowTop
    {
        get => _win.Top;
        set { if (_win.Top != value) { _win.Top = value; OnPropertyChanged(); } }
    }
    public double PanelWidth
    {
        get => _win.Width;
        set { var v = Math.Clamp(value, 160, 800); if (_win.Width != v) { _win.Width = v; OnPropertyChanged(); } }
    }
    public double PanelHeight
    {
        get => _win.Height;
        set { var v = Math.Clamp(value, 120, 900); if (_win.Height != v) { _win.Height = v; OnPropertyChanged(); } }
    }

    public void SaveGeometry() => _winStore.Save(_win);

    // ===== 可见性 =====
    [ObservableProperty] private bool _isBallVisible = true;

    [RelayCommand] private void ToggleVisibility() => IsBallVisible = !IsBallVisible;

    // ===== 设置 / 退出 =====
    private SettingsWindow? _settingsWindow;

    [RelayCommand]
    private void OpenSettings()
    {
        if (_settingsWindow != null) { _settingsWindow.Activate(); return; }
        _settingsWindow = (SettingsWindow)_services.GetService(typeof(SettingsWindow))!;
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    [RelayCommand]
    private void Exit() => System.Windows.Application.Current.Shutdown();
}

public sealed class WindowState
{
    public double Left { get; set; } = 0;
    public double Top { get; set; } = 300;
    public double Width { get; set; } = 260;
    public double Height { get; set; } = 360;
}
