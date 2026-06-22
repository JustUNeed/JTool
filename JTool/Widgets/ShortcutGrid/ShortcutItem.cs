namespace JTool.Widgets.ShortcutGrid;

public sealed class ShortcutItem
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
}

public sealed class ShortcutData
{
    public System.Collections.Generic.List<ShortcutItem> Items { get; set; } = new();
}
