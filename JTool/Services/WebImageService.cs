using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace JTool.Services;

public class WebImageService
{
    private static readonly HttpClient _http = new();

    /// <summary>下载 URL 为 BitmapSource（看板用）。失败返回 null。</summary>
    public async Task<BitmapSource?> DownloadBitmapAsync(string url)
    {
        try
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
        catch { return null; }
    }

    /// <summary>把拖入的图片（URL/位图）保存到目标目录。返回成功数。</summary>
    public async Task<int> SaveDroppedImageAsync(IDataObject data, string targetDir)
    {
        if (!Directory.Exists(targetDir)) return 0;

        // 直接位图
        if (data.GetDataPresent(DataFormats.Bitmap)
            && data.GetData(DataFormats.Bitmap) is BitmapSource bmp)
            return SaveBitmap(bmp, targetDir) ? 1 : 0;

        // URL（含 html/moz-url/text）
        string? url = ExtractUrl(data);
        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(url);
                string name = MakeFileName(url);
                string dest = EnsureUnique(Path.Combine(targetDir, name));
                await File.WriteAllBytesAsync(dest, bytes);
                return 1;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存图片失败：{ex.Message}", "JTool",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        return 0;
    }

    private static string? ExtractUrl(IDataObject data)
    {
        if (data.GetDataPresent("text/x-moz-url"))
        {
            var raw = data.GetData("text/x-moz-url") as string;
            var first = raw?.Split('\n')[0]?.Trim();
            if (IsHttp(first)) return first;
        }
        if (data.GetDataPresent(DataFormats.Html))
        {
            var html = data.GetData(DataFormats.Html) as string;
            var m = System.Text.RegularExpressions.Regex.Match(html ?? "",
                @"<img[^>]+src=[""']([^""']+)[""']",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success && IsHttp(m.Groups[1].Value)) return m.Groups[1].Value;
        }
        if (data.GetDataPresent(DataFormats.Text))
        {
            var t = (data.GetData(DataFormats.Text) as string)?.Trim();
            if (IsHttp(t)) return t;
        }
        return null;
    }

    private static bool SaveBitmap(BitmapSource bmp, string targetDir)
    {
        try
        {
            string dest = EnsureUnique(Path.Combine(targetDir,
                $"image_{DateTime.Now:yyyyMMdd_HHmmss}.png"));
            using var fs = new FileStream(dest, FileMode.Create);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            enc.Save(fs);
            return true;
        }
        catch { return false; }
    }

    private static bool IsHttp(string? s)
        => !string.IsNullOrWhiteSpace(s) && (s.StartsWith("http://") || s.StartsWith("https://"));

    private static string MakeFileName(string url)
    {
        try
        {
            var uri = new Uri(url);
            var name = Path.GetFileName(uri.LocalPath);
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
