using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media.Imaging;
using JTool.Models;

namespace JTool.Services;

/// <summary>看板独立持久化：board 目录存图片，board.json 存条目索引。</summary>
public class BoardService
{
    private static readonly string BoardDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "JTool", "board");
    private static readonly string IndexFile = Path.Combine(BoardDir, "board.json");
    private static readonly JsonSerializerOptions JsonOpt = new() { WriteIndented = true };

    public BoardService() => Directory.CreateDirectory(BoardDir);

    public List<BoardItem> Load()
    {
        try
        {
            if (File.Exists(IndexFile))
            {
                var list = JsonSerializer.Deserialize<List<BoardItem>>(File.ReadAllText(IndexFile));
                if (list != null)
                {
                    var result = new List<BoardItem>();
                    foreach (var it in list)
                    {
                        if (it.Type == BoardItemType.Image)
                        {
                            string abs = Path.Combine(BoardDir, it.ImagePath);
                            if (!File.Exists(abs)) continue;
                            it.ImagePath = abs;
                        }
                        result.Add(it);
                    }
                    return result;
                }
            }
        }
        catch { }
        return new List<BoardItem>();
    }

    public void Save(IEnumerable<BoardItem> items)
    {
        try
        {
            var toSave = new List<BoardItem>();
            foreach (var it in items)
            {
                toSave.Add(new BoardItem
                {
                    Type = it.Type,
                    Text = it.Text,
                    CreatedAt = it.CreatedAt,
                    ImagePath = it.Type == BoardItemType.Image ? Path.GetFileName(it.ImagePath) : ""
                });
            }
            File.WriteAllText(IndexFile, JsonSerializer.Serialize(toSave, JsonOpt));
        }
        catch { }
    }

    public BoardItem CreateTextItem(string text) => new()
    {
        Type = BoardItemType.Text,
        Text = text,
        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
    };

    public BoardItem? CreateImageItem(BitmapSource bmp)
    {
        try
        {
            string fileName = $"img_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            string dest = Path.Combine(BoardDir, fileName);
            using (var fs = new FileStream(dest, FileMode.Create))
            {
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bmp));
                enc.Save(fs);
            }
            return new BoardItem
            {
                Type = BoardItemType.Image,
                ImagePath = dest,
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存图片失败：{ex.Message}", "JTool",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
    }

    public void DeleteItemFile(BoardItem item)
    {
        try
        {
            if (item.Type == BoardItemType.Image && File.Exists(item.ImagePath))
                File.Delete(item.ImagePath);
        }
        catch { }
    }

    public void CopyText(string text) { try { Clipboard.SetText(text); } catch { } }

    public void CopyImage(string imagePath)
    {
        try
        {
            if (!File.Exists(imagePath)) return;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(imagePath);
            bmp.EndInit();
            bmp.Freeze();
            Clipboard.SetImage(bmp);
        }
        catch { }
    }
}
