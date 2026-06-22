using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JTool.Core;
using JTool.DragDrop;

using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace JTool.Widgets.TextBoard;

/// <summary>文本看板：自管文本数据、持久化，并贡献"添加文本到看板"投放槽。</summary>
public sealed partial class TextBoardViewModel : ObservableObject, IDropSlotProvider
{
  
    [ObservableProperty] private bool _isExpanded = true;

    [RelayCommand]
    private void Paste() => PasteFromClipboard();


    private readonly JsonStore<TextBoardData> _store = new("texts.json");
    private readonly TextBoardData _data;

    public ObservableCollection<TextBoardItemViewModel> Items { get; } = new();

    public TextBoardViewModel()
    {
        _data = _store.Load();
        foreach (var it in _data.Items)
            Items.Add(new TextBoardItemViewModel(it));
    }

    public bool HasContent => Items.Count > 0;

    [RelayCommand]
    private void Copy(TextBoardItemViewModel? vm)
    {
        if (vm == null) return;
        try { Clipboard.SetText(vm.Text); }
        catch (Exception ex) { Logger.Error("复制文本失败", ex); }
    }

    [RelayCommand]
    private void Remove(TextBoardItemViewModel? vm)
    {
        if (vm == null) return;
        Items.Remove(vm);
        _data.Items.Remove(vm.Model);
        Save();
        OnPropertyChanged(nameof(HasContent));
    }

    public void Add(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var model = new TextBoardItem { Text = text, CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
        _data.Items.Add(model);
        Items.Add(new TextBoardItemViewModel(model));
        Save();
        OnPropertyChanged(nameof(HasContent));
    }

    private void Save() => _store.Save(_data);

    // ===== 投放槽 =====
    // 仅当不是图片来源时，纯文本才进文本看板（图片来源交给图片看板）
    public System.Collections.Generic.IEnumerable<DropSlot> GetSlots(DropContext ctx)
    {
        if (ctx.HasText && !ctx.HasBitmap && !ctx.HasImageUrl)
            yield return new DropSlot { Title = "📌 文本到看板", OnDrop = c => Add(c.Text!) };
    }

    /// <summary>把剪贴板里的文本粘贴进看板。</summary>
    public void PasteFromClipboard()
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text)) Add(text);
                }
                return;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                System.Threading.Thread.Sleep(60);
            }
            catch (Exception ex)
            {
                Logger.Error("粘贴文本失败", ex);
                return;
            }
        }
    }

}
