using System;

namespace JTool.DragDrop;

/// <summary>一个投放落点：标题 + 落下时执行的动作。</summary>
public sealed class DropSlot
{
    public required string Title { get; init; }
    public required Action<DropContext> OnDrop { get; init; }
}

/// <summary>任何想在投放态贡献落点的模块都实现它（与 IPanelWidget 互相独立）。</summary>
public interface IDropSlotProvider
{
    /// <summary>根据当前拖入数据返回 0..N 个槽；不匹配则返回空。</summary>
    System.Collections.Generic.IEnumerable<DropSlot> GetSlots(DropContext ctx);
}
