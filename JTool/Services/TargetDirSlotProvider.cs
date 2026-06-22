// Services/TargetDirSlotProvider.cs
using System.Collections.Generic;
using JTool.DragDrop;

namespace JTool.Services;

/// <summary>把每个目标目录暴露成投放槽：拖文件来→搬运；拖图片来→下载保存。</summary>
public sealed class TargetDirSlotProvider : IDropSlotProvider
{
    private readonly TargetDirStore _dirs;
    private readonly FileMoveService _move;
    private readonly WebImageService _web;

    public TargetDirSlotProvider(TargetDirStore dirs, FileMoveService move, WebImageService web)
    {
        _dirs = dirs; _move = move; _web = web;
    }

    public IEnumerable<DropSlot> GetSlots(DropContext ctx)
    {
        // 登记新目录的槽
        if (ctx.HasFolders)
            yield return new DropSlot
            {
                Title = "📁 登记为目标目录",
                OnDrop = c => _dirs.Add(c.Folders)
            };

        if (!ctx.HasFiles && !ctx.HasImageSource) yield break;

        foreach (var dir in _dirs.Dirs)
        {
            string captured = dir;
            yield return new DropSlot
            {
                Title = "→ " + DropContext.SafeName(dir),
                OnDrop = c => HandleDrop(c, captured)
            };
        }
    }

    private async void HandleDrop(DropContext c, string dir)
    {
        try
        {
            if (c.HasFiles) _move.MoveToDirectory(c.Files, dir);
            else if (c.HasImageUrl) await _web.DownloadToFileAsync(c.ImageUrl!, dir);
        }
        catch (System.Exception ex) { Core.Logger.Error("投放到目录失败", ex); }
    }
}
