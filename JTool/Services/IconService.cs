using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using JTool.Core;

namespace JTool.Services;

/// <summary>提取文件/目录图标。并发安全 + 容量上限。</summary>
public sealed class IconService
{
    private const int MaxEntries = 512;
    private readonly ConcurrentDictionary<string, BitmapSource?> _cache = new();

    public BitmapSource? GetIcon(string path, bool large = true)
    {
        string key = (large ? "L:" : "S:") + path;
        if (_cache.TryGetValue(key, out var cached)) return cached;

        if (_cache.Count > MaxEntries) _cache.Clear();   // 简单容量保护

        var icon = Extract(path, large);
        _cache[key] = icon;
        return icon;
    }

    public void Invalidate(string path)
    {
        _cache.TryRemove("L:" + path, out _);
        _cache.TryRemove("S:" + path, out _);
    }

    private static BitmapSource? Extract(string path, bool large)
    {
        try
        {
            var shinfo = new NativeMethods.SHFILEINFO();
            uint flags = NativeMethods.SHGFI_ICON |
                (large ? NativeMethods.SHGFI_LARGEICON : NativeMethods.SHGFI_SMALLICON);

            bool exists = System.IO.File.Exists(path) || System.IO.Directory.Exists(path);
            if (!exists) flags |= NativeMethods.SHGFI_USEFILEATTRIBUTES;

            uint attr = System.IO.Directory.Exists(path)
                ? NativeMethods.FILE_ATTRIBUTE_DIRECTORY
                : NativeMethods.FILE_ATTRIBUTE_NORMAL;

            IntPtr res = NativeMethods.SHGetFileInfo(
                path, attr, ref shinfo,
                (uint)Marshal.SizeOf(typeof(NativeMethods.SHFILEINFO)), flags);

            if (res == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero) return null;

            try
            {
                var bmp = Imaging.CreateBitmapSourceFromHIcon(
                    shinfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bmp.Freeze();
                return bmp;
            }
            finally { NativeMethods.DestroyIcon(shinfo.hIcon); }
        }
        catch (Exception ex) { Logger.Error($"提取图标失败: {path}", ex); return null; }
    }
}
