using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace JTool.DragDrop;

/// <summary>解析拖入数据，并汇总所有 provider 在该数据下贡献的投放槽。</summary>
public sealed class DropRouter
{
    private readonly IReadOnlyList<IDropSlotProvider> _providers;

    public DropRouter(IEnumerable<IDropSlotProvider> providers)
        => _providers = providers.ToList();

    public bool CanAccept(IDataObject data) => DropParser.CanAccept(data);

    public DropContext Parse(IDataObject data) => DropParser.Parse(data);

    public IReadOnlyList<DropSlot> CollectSlots(DropContext ctx)
        => _providers.SelectMany(p => p.GetSlots(ctx)).ToList();
}
