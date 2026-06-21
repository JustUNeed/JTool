namespace JTool.Models;

public enum BoardItemType { Text, Image }

public class BoardItem
{
    public BoardItemType Type { get; set; }
    public string Text { get; set; } = "";        // Type=Text 时有效
    public string ImagePath { get; set; } = "";   // Type=Image：内存中为绝对路径，存盘时为文件名
    public string CreatedAt { get; set; } = "";
}
