namespace JTool.Widgets.TextBoard;

public sealed class TextBoardItem
{
    public string Text { get; set; } = "";
    public string CreatedAt { get; set; } = "";
}

public sealed class TextBoardData
{
    public System.Collections.Generic.List<TextBoardItem> Items { get; set; } = new();
}
