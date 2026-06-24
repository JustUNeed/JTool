using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JTool.Core;
using JTool.Hosting;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;

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
        set
        {
            var v = Math.Clamp(value, 16, 100);
            if (Math.Abs(_s.BallSize - v) > 0.01) { _s.BallSize = v; OnPropertyChanged(); _service.Save(); }
        }
    }

    public bool EnableImageDownload
    {
        get => _s.EnableImageDownload;
        set { if (_s.EnableImageDownload != value) { _s.EnableImageDownload = value; OnPropertyChanged(); } }
    }

    // ===== 悬浮球外观（实时 + 即时存盘） =====
    public string BallColor
    {
        get => _s.BallColor;
        set { if (_s.BallColor != value) { _s.BallColor = value; OnPropertyChanged(); _service.Save(); } }
    }

    public string BallText
    {
        get => _s.BallText;
        set { if (_s.BallText != value) { _s.BallText = value ?? ""; OnPropertyChanged(); _service.Save(); } }
    }

    public double BallCornerRadius
    {
        get => _s.BallCornerRadius;
        set
        {
            var v = Math.Clamp(value, 0, 100);
            if (Math.Abs(_s.BallCornerRadius - v) > 0.01) { _s.BallCornerRadius = v; OnPropertyChanged(); _service.Save(); }
        }
    }

    public string BallImagePath
    {
        get => _s.BallImagePath;
        set
        {
            if (_s.BallImagePath != value)
            {
                _s.BallImagePath = value;
                OnPropertyChanged();
                _service.Save();
            }
        }
    }

    // ===== 命令 =====
    [RelayCommand]
    private void Save() => _service.Save();

    [RelayCommand]
    private void PickBallImage()
    {
        var dlg = new OpenFileDialog
        {
            Title = "选择悬浮球图片",
            Filter = "图片 (*.png;*.jpg;*.jpeg;*.bmp;*.ico)|*.png;*.jpg;*.jpeg;*.bmp;*.ico"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            // 解码目标边长：球大小的 2 倍，兼顾高 DPI 清晰度，下限 64
            int target = (int)Math.Max(64, _s.BallSize * 2);

            // 读取并按目标尺寸解码（DecodePixelWidth 让解码阶段就缩小，省内存）
            var src = new BitmapImage();
            src.BeginInit();
            src.CacheOption = BitmapCacheOption.OnLoad;
            src.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            src.UriSource = new Uri(dlg.FileName);
            src.DecodePixelWidth = target;   // 等比缩放，按宽缩
            src.EndInit();
            src.Freeze();

            // 编码为 PNG（保留透明通道），用带时间戳的唯一文件名避免缓存命中旧图
            var dest = Paths.File($"ball_icon_{DateTime.Now:yyyyMMddHHmmssfff}.png");
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(src));
            using (var fs = File.Create(dest))
                encoder.Save(fs);

            // 删除上一张旧缩略图，避免 AppData 堆积
            var old = _s.BallImagePath;
            BallImagePath = dest;
            if (!string.IsNullOrWhiteSpace(old) && File.Exists(old) &&
                !string.Equals(old, dest, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(old); } catch { /* 删不掉就算了 */ }
            }
        }
        catch (Exception ex)
        {
            Logger.Error("设置悬浮球图片失败", ex);
        }
    }
    [RelayCommand]
    private void ClearBallImage()
    {
        var old = _s.BallImagePath;
        BallImagePath = "";
        if (!string.IsNullOrWhiteSpace(old) && File.Exists(old))
        {
            try { File.Delete(old); } catch { }
        }
    }

    [RelayCommand]
    private void OpenConfigFolder()
    {
        try { Process.Start(new ProcessStartInfo { FileName = Paths.Root, UseShellExecute = true }); }
        catch (Exception ex) { Logger.Error("打开配置目录失败", ex); }
    }

    [RelayCommand]
    private void ResetWindow()
    {
        try
        {
            var store = new JsonStore<WindowState>("window.json");
            store.Save(new WindowState());
            Logger.Info("窗口设置已重置，重启后生效");
        }
        catch (Exception ex) { Logger.Error("重置窗口设置失败", ex); }
    }
}
