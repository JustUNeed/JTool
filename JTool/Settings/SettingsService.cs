using System;
using Microsoft.Win32;
using JTool.Core;

namespace JTool.Settings;

/// <summary>设置的加载/保存 + 开机自启注册表落地。</summary>
public sealed class SettingsService
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "JTool";

    private readonly JsonStore<AppSettings> _store = new("settings.json");
    public AppSettings Current { get; }

    public SettingsService()
    {
        Current = _store.Load();
        ApplyAutoStart(Current.AutoStart);
    }

    public void Save()
    {
        _store.Save(Current);
        ApplyAutoStart(Current.AutoStart);
    }

    private static void ApplyAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            if (enable)
                key.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
            else if (key.GetValue(AppName) != null)
                key.DeleteValue(AppName, throwOnMissingValue: false);
        }
        catch (Exception ex) { Logger.Error("设置开机自启失败", ex); }
    }
}
