using System;
using System.IO;
using System.Windows.Shapes;

namespace JTool.Core;

/// <summary>统一日志，替代散落各处的空 catch。线程安全，失败不抛。</summary>
public static class Logger
{
    private static readonly string LogFile = System.IO.Path.Combine(Paths.Root, "log.txt");
    private static readonly object Gate = new();

    public static void Info(string msg) => Write("INFO", msg);
    public static void Warn(string msg) => Write("WARN", msg);

    public static void Error(string msg, Exception? ex = null)
        => Write("ERROR", ex == null ? msg : $"{msg} :: {ex}");

    private static void Write(string level, string msg)
    {
        try
        {
            Directory.CreateDirectory(Paths.Root);
            lock (Gate)
                File.AppendAllText(LogFile,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}{Environment.NewLine}");
        }
        catch { /* 日志本身失败时无能为力，静默 */ }
    }
}
