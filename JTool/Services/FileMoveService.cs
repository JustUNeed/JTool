using System.IO;
using Microsoft.VisualBasic.FileIO;
using JTool.Core;

namespace JTool.Services;

/// <summary>文件/目录搬运。改用 VB.FileSystem，带系统进度对话框，无命令注入面。</summary>
public sealed class FileMoveService
{
    public void MoveToDirectory(string[] paths, string targetDir)
    {
        if (paths is null || paths.Length == 0) return;
        if (!Directory.Exists(targetDir))
        {
            Logger.Warn($"目标目录不存在: {targetDir}");
            return;
        }

        foreach (var src in paths)
        {
            try
            {
                if (Directory.Exists(src))
                {
                    string dest = Path.Combine(targetDir, new DirectoryInfo(src.TrimEnd('\\')).Name);
                    FileSystem.MoveDirectory(src, dest, UIOption.AllDialogs);
                }
                else if (File.Exists(src))
                {
                    string dest = Path.Combine(targetDir, Path.GetFileName(src));
                    FileSystem.MoveFile(src, dest, UIOption.AllDialogs);
                }
            }
            catch (System.Exception ex) { Logger.Error($"移动失败: {src} -> {targetDir}", ex); }
        }
    }
}
