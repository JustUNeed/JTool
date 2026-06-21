using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace JTool.Services;

/// <summary>
/// 把移动交给独立的 PowerShell 进程执行（Shell.Application.MoveHere）。
/// JTool 启动它后即脱手；关闭 JTool 不会中断移动，且有系统进度对话框。
/// </summary>
public class FileOperationService
{
    private const int MoveFlags = 0; // 系统默认交互（有进度、有冲突询问）

    public void MoveToDirectory(string[] paths, string targetDir)
    {
        if (paths == null || paths.Length == 0) return;
        if (!Directory.Exists(targetDir))
        {
            MessageBox.Show($"目标目录不存在：\n{targetDir}", "JTool",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string script = BuildScript(paths, targetDir);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command " + script,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法启动移动进程：{ex.Message}", "JTool",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string BuildScript(string[] paths, string targetDir)
    {
        var sb = new StringBuilder();
        sb.Append("\"");
        sb.Append("$sh = New-Object -ComObject Shell.Application; ");
        sb.Append($"$dst = $sh.NameSpace('{Escape(targetDir)}'); ");
        foreach (var p in paths)
        {
            string parent = Path.GetDirectoryName(p) ?? "";
            string leaf = Path.GetFileName(p.TrimEnd('\\'));
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) continue;
            sb.Append($"$src = $sh.NameSpace('{Escape(parent)}'); ");
            sb.Append($"$item = $src.ParseName('{Escape(leaf)}'); ");
            sb.Append($"if ($item) {{ $dst.MoveHere($item, {MoveFlags}) }}; ");
        }
        sb.Append("\"");
        return sb.ToString();
    }

    private static string Escape(string s) => s.Replace("'", "''");
}
