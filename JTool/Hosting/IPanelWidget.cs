namespace JTool.Hosting;

/// <summary>能放进快捷面板的控件契约。宿主只跟接口打交道。</summary>
public interface IPanelWidget
{
    string Title { get; }            // 折叠区标题
    bool HasContent { get; }         // 是否有内容（空可不显示）
    object View { get; }             // 该控件的 UI（UserControl 实例）
}
