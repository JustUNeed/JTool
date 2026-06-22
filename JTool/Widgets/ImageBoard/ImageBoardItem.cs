namespace JTool.Widgets.ImageBoard;

public sealed class ImageBoardItem
{
    public string FileName { get; set; } = "";        // 仅文件名，存于 board/images
    public string CreatedAt { get; set; } = "";
}

public sealed class ImageBoardData
{
    public System.Collections.Generic.List<ImageBoardItem> Items { get; set; } = new();
}

public enum ImageLoadState { Ready, Loading, Failed }
