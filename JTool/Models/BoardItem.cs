namespace JTool.Models;

public enum BoardItemType { Text, Image }

/// <summary>图片项加载状态。Loading / Failed 仅存在于内存，不持久化。</summary>
public enum BoardLoadState { Ready, Loading, Failed }

public class BoardItem
{
    public BoardItemType Type { get; set; }
    public string Text { get; set; } = "";        // Type=Text 时有效
    public string ImagePath { get; set; } = "";   // 内存中为绝对路径，存盘时为文件名
    public string CreatedAt { get; set; } = "";
}
