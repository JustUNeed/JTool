using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;

namespace JTool.Services;

/// <summary>统一解析拖入的 IDataObject，避免 View / Service 各写一套。</summary>
public static class DragDataParser
{
    private static readonly string[] ImageExts =
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

    public static string[] GetFiles(IDataObject data)
        => data.GetDataPresent(DataFormats.FileDrop)
            ? (string[])data.GetData(DataFormats.FileDrop)!
            : Array.Empty<string>();

    public static bool HasFolder(IDataObject data)
        => GetFiles(data).Any(Directory.Exists);

    public static BitmapSource? GetBitmap(IDataObject data)
        => data.GetDataPresent(DataFormats.Bitmap)
            ? data.GetData(DataFormats.Bitmap) as BitmapSource
            : null;

    /// <summary>从 moz-url / html&lt;img&gt; / 纯文本里依次提取一段文本（通常是 URL 或正文）。</summary>
    public static string? GetText(IDataObject data)
    {
        if (data.GetDataPresent("text/x-moz-url"))
        {
            var first = (data.GetData("text/x-moz-url") as string)?
                .Split('\n').FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(first)) return first;
        }
        if (data.GetDataPresent(DataFormats.Html))
        {
            var html = data.GetData(DataFormats.Html) as string;
            var m = Regex.Match(html ?? "", @"<img[^>]+src=[""']([^""']+)[""']",
                RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value;
        }
        if (data.GetDataPresent(DataFormats.Text))
            return (data.GetData(DataFormats.Text) as string)?.Trim();
        return null;
    }

    /// <summary>解析出可下载的 http(s) 图片 URL；不是图片 URL 返回 null。</summary>
    public static string? GetImageUrl(IDataObject data)
    {
        var text = GetText(data);
        return IsImageUrl(text) ? text : null;
    }

    public static bool IsHttp(string? s)
        => !string.IsNullOrWhiteSpace(s)
        && (s.StartsWith("http://") || s.StartsWith("https://"));

    public static bool IsImageUrl(string? s)
    {
        if (!IsHttp(s)) return false;
        var lower = s!.ToLowerInvariant();
        return ImageExts.Any(lower.Contains)
            || lower.Contains("image") || lower.Contains("/img")
            // 常见无扩展名图床 / CDN 特征
            || lower.Contains("bing.net/th")     // Bing 缩略图
            || lower.Contains("/th/id/")
            || lower.Contains("googleusercontent")
            || lower.Contains("sinaimg")         // 微博图床
            || lower.Contains("pic") && lower.Contains("?");
    }

    /// <summary>
    /// "可能是图片"：是 http 链接、且不像普通网页（无 .html/.php 等页面后缀）。
    /// 用于无法从 URL 确定时，先乐观当图片下载，解码失败再退回文本。
    /// </summary>
    public static bool IsProbablyImageUrl(string? s)
    {
        if (!IsHttp(s)) return false;
        if (IsImageUrl(s)) return true;
        var lower = s!.ToLowerInvariant();
        // 明显是网页/文档的就不当图片
        string[] pageExts = { ".html", ".htm", ".php", ".asp", ".aspx", ".jsp" };
        foreach (var ext in pageExts)
            if (lower.Contains(ext)) return false;
        return true;   // 其余 http 链接都先试一次
    }


    /// <summary>是否包含任何本应用可接收的数据。</summary>
    public static bool CanAccept(IDataObject data)
        => data.GetDataPresent(DataFormats.FileDrop)
        || data.GetDataPresent(DataFormats.Text)
        || data.GetDataPresent(DataFormats.Html)
        || data.GetDataPresent("text/x-moz-url")
        || data.GetDataPresent(DataFormats.Bitmap);
}
