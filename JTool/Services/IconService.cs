using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using JTool.Helpers;

namespace JTool.Services;

public class IconService
{
    private readonly Dictionary<string, BitmapSource?> _cache = new();

    public BitmapSource? GetIcon(string path, bool large)
    {
        string key = (large ? "L:" : "S:") + path;
        if (_cache.TryGetValue(key, out var cached)) return cached;
        var icon = Extract(path, large);
        _cache[key] = icon;
        return icon;
    }

    public void Invalidate(string path)
    {
        _cache.Remove("L:" + path);
        _cache.Remove("S:" + path);
    }

    private static BitmapSource? Extract(string path, bool large)
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
        catch { return null; }
        finally { NativeMethods.DestroyIcon(shinfo.hIcon); }
    }
}
