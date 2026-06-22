using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using JTool.Core;

namespace JTool.Services;

/// <summary>网络图片下载。带超时、大小上限、Content-Type 校验。只做 IO，不弹 UI。</summary>
public sealed class WebImageService
{
    private const long MaxBytes = 20 * 1024 * 1024;          // 20MB 上限
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private static readonly HttpClient Http = new() { Timeout = Timeout };

    /// <summary>下载并解码为 BitmapSource。失败抛异常，由调用方处理。</summary>
    public async Task<BitmapSource> DownloadBitmapAsync(string url, CancellationToken ct = default)
    {
        var bytes = await DownloadBytesAsync(url, requireImage: true, ct);
        using var ms = new MemoryStream(bytes);
        var bmp = new BitmapImage();
        bmp.BeginInit();
        bmp.CacheOption = BitmapCacheOption.OnLoad;
        bmp.StreamSource = ms;
        bmp.EndInit();
        bmp.Freeze();
        return bmp;
    }

    /// <summary>下载图片字节并写入目标目录，返回保存的完整路径。</summary>
    public async Task<string> DownloadToFileAsync(string url, string targetDir,
        CancellationToken ct = default)
    {
        var bytes = await DownloadBytesAsync(url, requireImage: true, ct);
        string dest = EnsureUnique(Path.Combine(targetDir, MakeFileName(url)));
        await File.WriteAllBytesAsync(dest, bytes, ct);
        return dest;
    }

    private static async Task<byte[]> DownloadBytesAsync(string url, bool requireImage,
        CancellationToken ct)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        if (requireImage)
        {
            var type = resp.Content.Headers.ContentType?.MediaType ?? "";
            if (!type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"非图片内容: {type}");
        }

        var len = resp.Content.Headers.ContentLength;
        if (len is > MaxBytes)
            throw new InvalidOperationException($"图片过大: {len} 字节");

        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        if (bytes.Length > MaxBytes)
            throw new InvalidOperationException($"图片过大: {bytes.Length} 字节");
        return bytes;
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
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
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
