using System.Collections.Generic;

namespace JTool.Models;

public class GridConfig
{
    public int Columns { get; set; } = 3;
    public int MaxRows { get; set; } = 0;        // 0 = 无限
    public double CellWidth { get; set; } = 64;
    public double CellHeight { get; set; } = 64;
    public double IconSize { get; set; } = 24;
}

public class LauncherConfig
{
    public GridConfig Grid { get; set; } = new();
    public List<ShortcutItem> Shortcuts { get; set; } = new();
    public List<string> TargetDirs { get; set; } = new();
    public double WindowLeft { get; set; } = 0;
    public double WindowTop { get; set; } = 300;
}
