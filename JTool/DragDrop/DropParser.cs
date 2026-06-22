using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;

namespace JTool.DragDrop;

/// <summary>IDataObject → DropContext。吸收原 DragDataParser 的全部解析规则。</summary>
public static class DropParser
{
    private static readonly string[] ImageExts =
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

    public static bool CanAccept(IDataObject d)
        => d.GetDataPresent(DataFormats.FileDrop)
        || d.GetDataPresent(DataFormats.Text)
        || d.GetDataPresent(DataFormats.Html)
        || d.GetDataPresent("text/x-moz-url")
        || d.GetDataPresent(DataFormats.Bitmap);

    public static DropContext Parse(IDataObject d)
    {
        var files = GetFiles(d);
        var text = GetText(d);
        return new DropContext
        {
            Files = files,
            Folders = files.Where(Directory.Exists).ToArray(),
            Bitmap = GetBitmap(d),
            Text = text,
            ImageUrl = IsImageUrl(text) || IsProbablyImageUrl(text) ? text : null,
        };
    }

    private static string[] GetFiles(IDataObject d)
        => d.GetDataPresent(DataFormats.FileDrop)
            ? (string[])d.GetData(DataFormats.FileDrop)!
            : Array.Empty<string>();

    private static BitmapSource? GetBitmap(IDataObject d)
        => d.GetDataPresent(DataFormats.Bitmap)
            ? d.GetData(DataFormats.Bitmap) as BitmapSource
            : null;

    private static string? GetText(IDataObject d)
    {
        if (d.GetDataPresent("text/x-moz-url"))
        {
            var first = (d.GetData("text/x-moz-url") as string)?
                .Split('\n').FirstOrDefault()?.Trim();
            if (!string.IsNullOrWhiteSpace(first)) return first;
        }
        if (d.GetDataPresent(DataFormats.Html))
        {
            var html = d.GetData(DataFormats.Html) as string;
            var m = Regex.Match(html ?? "", @"<img[^>]+src=[""']([^""']+)[""']",
                RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value;
        }
        if (d.GetDataPresent(DataFormats.Text))
            return (d.GetData(DataFormats.Text) as string)?.Trim();
        return null;
    }

    public static bool IsHttp(string? s)
        => !string.IsNullOrWhiteSpace(s)
        && (s.StartsWith("http://") || s.StartsWith("https://"));

    private static bool IsImageUrl(string? s)
    {
        if (!IsHttp(s)) return false;
        var lower = s!.ToLowerInvariant();
        return ImageExts.Any(lower.Contains)
            || lower.Contains("image") || lower.Contains("/img")
            || lower.Contains("bing.net/th") || lower.Contains("/th/id/")
            || lower.Contains("googleusercontent") || lower.Contains("sinaimg")
            || (lower.Contains("pic") && lower.Contains("?"));
    }

    private static bool IsProbablyImageUrl(string? s)
    {
        if (!IsHttp(s)) return false;
        var lower = s!.ToLowerInvariant();
        string[] pageExts = { ".html", ".htm", ".php", ".asp", ".aspx", ".jsp" };
        return !pageExts.Any(lower.Contains);
    }
}
