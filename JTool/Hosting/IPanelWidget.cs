using System.ComponentModel;

namespace JTool.Hosting;

/// <summary>能放进快捷面板的控件契约。宿主只跟接口打交道。</summary>
public interface IPanelWidget : INotifyPropertyChanged
{
    string Title { get; }
    bool HasContent { get; }
    object View { get; }

    bool IsExpanded { get; set; }   // 折叠状态
}

/// <summary>可选：widget 若支持"粘贴剪贴板"，实现此接口，宿主会在折叠头右侧显示粘贴按钮。</summary>
public interface IPasteTarget
{
    void PasteFromClipboard();
}
