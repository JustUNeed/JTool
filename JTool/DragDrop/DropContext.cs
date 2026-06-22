using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace JTool.DragDrop;

/// <summary>把原始 IDataObject 解析后的强类型结果。控件只读这里，不再碰 IDataObject。</summary>
public sealed class DropContext
{
    public string[] Files { get; init; } = Array.Empty<string>();
    public string[] Folders { get; init; } = Array.Empty<string>();
    public BitmapSource? Bitmap { get; init; }
    public string? ImageUrl { get; init; }   // 形如图片的 http(s) 链接
    public string? Text { get; init; }       // 任意拖入文本（可能是普通文字或 URL）

    public bool HasFiles => Files.Length > 0;
    public bool HasFolders => Folders.Length > 0;
    public bool HasBitmap => Bitmap != null;
    public bool HasImageUrl => !string.IsNullOrWhiteSpace(ImageUrl);
    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    /// <summary>能保存为图片的来源（位图或图片 URL）。</summary>
    public bool HasImageSource => HasBitmap || HasImageUrl;

    public bool IsEmpty => !HasFiles && !HasBitmap && !HasText;

    public static string SafeName(string path)
    {
        try
        {
            return File.Exists(path)
                ? Path.GetFileNameWithoutExtension(path)
                : new DirectoryInfo(path.TrimEnd('\\')).Name;
        }
        catch { return path; }
    }

    private static readonly string[] ImageFileExts =
    { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

    /// <summary>拖入文件中属于本地图片的那些。</summary>
    public string[] ImageFiles =>
        Files.Where(f =>
            System.IO.File.Exists(f) &&
            ImageFileExts.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()))
        .ToArray();

    public bool HasImageFiles => ImageFiles.Length > 0;

}
