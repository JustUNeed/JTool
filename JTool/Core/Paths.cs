using System;
using System.IO;

namespace JTool.Core;

/// <summary>集中管理所有持久化路径，全部位于 %AppData%\JTool。</summary>
public static class Paths
{
    public static readonly string Root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JTool");

    public static readonly string BoardImagesDir = Path.Combine(Root, "board", "images");

    public static string File(string name) => Path.Combine(Root, name);

    static Paths()
    {
        try
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(BoardImagesDir);
        }
        catch (Exception ex) { Logger.Error("创建数据目录失败", ex); }
    }
}
