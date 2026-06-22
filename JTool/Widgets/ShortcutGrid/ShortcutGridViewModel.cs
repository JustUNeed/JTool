using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GongSolutions.Wpf.DragDrop;
using JTool.Core;
using JTool.DragDrop;
using JTool.Services;

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace JTool.Widgets.ShortcutGrid;

/// <summary>快捷网格：自管数据、排序、刷新、持久化，并贡献"添加快捷方式"投放槽。</summary>
public sealed partial class ShortcutGridViewModel : ObservableObject, IDropTarget, IDropSlotProvider
{
    [ObservableProperty] private bool _isExpanded = true;

    private readonly IconService _icons;
    private readonly JsonStore<ShortcutData> _store = new("shortcuts.json");
    private readonly ShortcutData _data;
    private readonly DefaultDropHandler _defaultDrop = new();

    public ObservableCollection<ShortcutItemViewModel> Items { get; } = new();

    public ShortcutGridViewModel(IconService icons)
    {
        _icons = icons;
        _data = _store.Load();
        foreach (var it in _data.Items)
            Items.Add(new ShortcutItemViewModel(it, _icons));
    }

    public bool HasContent => Items.Count > 0;

    [RelayCommand]
    private void Launch(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex) { Logger.Error($"启动失败: {path}", ex); }
    }

    [RelayCommand]
    private void Remove(ShortcutItemViewModel? vm)
    {
        if (vm == null) return;
        Items.Remove(vm);
        _data.Items.Remove(vm.Model);
        _icons.Invalidate(vm.Path);
        Save();
    }

    public void Add(string[] paths)
    {
        foreach (var path in paths)
        {
            if (_data.Items.Any(s => s.Path == path)) continue;
            string name = File.Exists(path)
                ? Path.GetFileNameWithoutExtension(path)
                : DropContext.SafeName(path);
            var model = new ShortcutItem { Name = name, Path = path };
            _data.Items.Add(model);
            Items.Add(new ShortcutItemViewModel(model, _icons));
        }
        Save();
    }

    // ===== 拖拽排序（gong-wpf-dragdrop）=====
    public void DragOver(IDropInfo dropInfo) => _defaultDrop.DragOver(dropInfo);

    public void Drop(IDropInfo dropInfo)
    {
        _defaultDrop.Drop(dropInfo);
        SyncOrder();
        Save();
    }

    private void SyncOrder()
    {
        _data.Items.Clear();
        _data.Items.AddRange(Items.Select(v => v.Model));
    }

    private void Save() => _store.Save(_data);

    // ===== 投放槽 =====
    public System.Collections.Generic.IEnumerable<DropSlot> GetSlots(DropContext ctx)
    {
        if (ctx.HasFiles)
            yield return new DropSlot { Title = "＋ 添加快捷方式", OnDrop = c => Add(c.Files) };
    }
}
