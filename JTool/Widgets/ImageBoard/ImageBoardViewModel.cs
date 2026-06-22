using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JTool.Core;
using JTool.DragDrop;
using JTool.Services;
using JTool.Settings;

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace JTool.Widgets.ImageBoard;

/// <summary>图片看板：自管图片数据、下载、持久化，并贡献"添加图片到看板"投放槽。</summary>
public sealed partial class ImageBoardViewModel : ObservableObject, IDropSlotProvider
{
    private readonly WebImageService _web;
    private readonly AppSettings _settings;
    private readonly JsonStore<ImageBoardData> _store = new("images.json");
    private readonly ImageBoardData _data;

    public ObservableCollection<ImageBoardItemViewModel> Items { get; } = new();

    public ImageBoardViewModel(WebImageService web, AppSettings settings)
    {
        _web = web;
        _settings = settings;
        _data = _store.Load();
        foreach (var it in _data.Items.Where(i => File.Exists(Path.Combine(Paths.BoardImagesDir, i.FileName))))
            Items.Add(new ImageBoardItemViewModel(it));
    }

    public bool HasContent => Items.Count > 0;

    [RelayCommand]
    private void Copy(ImageBoardItemViewModel? vm)
    {
        if (vm == null || !vm.IsReady) return;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(vm.AbsolutePath);
            bmp.EndInit();
            bmp.Freeze();
            Clipboard.SetImage(bmp);
        }
        catch (Exception ex) { Logger.Error("复制图片失败", ex); }
    }

    [RelayCommand]
    private void Remove(ImageBoardItemViewModel? vm)
    {
        if (vm == null) return;
        Items.Remove(vm);
        _data.Items.Remove(vm.Model);
        try { if (File.Exists(vm.AbsolutePath)) File.Delete(vm.AbsolutePath); }
        catch (Exception ex) { Logger.Error("删除图片文件失败", ex); }
        Save();
        OnPropertyChanged(nameof(HasContent));
    }

    public void AddBitmap(BitmapSource bmp)
    {
        var fileName = SaveBitmap(bmp);
        if (fileName == null) return;
        AddModel(new ImageBoardItem { FileName = fileName, CreatedAt = Now() });
    }

    public async Task AddFromUrlAsync(string url)
    {
        var model = new ImageBoardItem { CreatedAt = Now() };
        var vm = new ImageBoardItemViewModel(model) { State = ImageLoadState.Loading };
        Items.Add(vm);
        OnPropertyChanged(nameof(HasContent));

        try
        {
            var bmp = await _web.DownloadBitmapAsync(url);
            var fileName = SaveBitmap(bmp);
            if (fileName == null) throw new Exception("保存失败");
            _data.Items.Add(model);
            vm.SetReady(fileName);
            Save();
        }
        catch (Exception ex)
        {
            Logger.Error($"看板图片下载失败: {url}", ex);
            vm.State = ImageLoadState.Failed;
        }
    }

    private void AddModel(ImageBoardItem model)
    {
        _data.Items.Add(model);
        Items.Add(new ImageBoardItemViewModel(model));
        Save();
        OnPropertyChanged(nameof(HasContent));
    }

    private static string? SaveBitmap(BitmapSource bmp)
    {
        try
        {
            string fileName = $"img_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            string dest = Path.Combine(Paths.BoardImagesDir, fileName);
            using var fs = new FileStream(dest, FileMode.Create);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            enc.Save(fs);
            return fileName;
        }
        catch (Exception ex) { Logger.Error("保存看板图片失败", ex); return null; }
    }

    private void Save() => _store.Save(_data);
    private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm");

    // ===== 投放槽 =====
    public System.Collections.Generic.IEnumerable<DropSlot> GetSlots(DropContext ctx)
    {
        if (ctx.HasBitmap)
            yield return new DropSlot { Title = "🖼 图片到看板", OnDrop = c => AddBitmap(c.Bitmap!) };
        else if (ctx.HasImageUrl && _settings.EnableImageDownload)
            yield return new DropSlot
            {
                Title = "🖼 网络图片到看板",
                OnDrop = async c => await AddFromUrlAsync(c.ImageUrl!)
            };
    }
}
