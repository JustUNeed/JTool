using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace JTool.Services;

/// <summary>图片下载与保存。只做 IO，不弹 UI、不解析拖拽数据。</summary>
public class WebImageService
{
    private static readonly HttpClient _http = new();

    /// <summary>下载 URL 为 BitmapSource。失败会抛异常，由调用方决定如何处理。</summary>
    public async Task<BitmapSource> DownloadBitmapAsync(string url)
    {
        var bytes = await _http.GetByteArrayAsync(url);
        using var ms = new MemoryStream(bytes);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    /// <summary>把拖入的图片（位图或 URL）保存到目标目录，返回成功数。失败抛异常。</summary>
    public async Task<int> SaveDroppedImageAsync(IDataObject data, string targetDir)
    {
        if (!Directory.Exists(targetDir)) return 0;

        var bmp = DragDataParser.GetBitmap(data);
        if (bmp != null)
            return SaveBitmap(bmp, targetDir) ? 1 : 0;

        // 优先图片 URL，其次任意 http 文本
        string? url = DragDataParser.GetImageUrl(data);
        if (string.IsNullOrEmpty(url))
        {
            var text = DragDataParser.GetText(data);
            if (DragDataParser.IsHttp(text)) url = text;
        }
        if (string.IsNullOrEmpty(url)) return 0;

        var bytes = await _http.GetByteArrayAsync(url);
        string dest = EnsureUnique(Path.Combine(targetDir, MakeFileName(url)));
        await File.WriteAllBytesAsync(dest, bytes);
        return 1;
    }

    private static bool SaveBitmap(BitmapSource bmp, string targetDir)
    {
        string dest = EnsureUnique(Path.Combine(targetDir,
            $"image_{DateTime.Now:yyyyMMdd_HHmmss}.png"));
        using var fs = new FileStream(dest, FileMode.Create);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        enc.Save(fs);
        return true;
    }

    private static string MakeFileName(string url)
    {
        try
        {
            var name = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrWhiteSpace(name) || !Path.HasExtension(name))
                name = $"image_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            return Sanitize(name);
        }
        catch { return $"image_{DateTime.Now:yyyyMMdd_HHmmss}.jpg"; }
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static string EnsureUnique(string path)
    {
        if (!File.Exists(path)) return path;
        string dir = Path.GetDirectoryName(path)!;
        string b = Path.GetFileNameWithoutExtension(path);
        string ext = Path.GetExtension(path);
        int i = 1; string c;
        do { c = Path.Combine(dir, $"{b} ({i++}){ext}"); } while (File.Exists(c));
        return c;
    }
}
