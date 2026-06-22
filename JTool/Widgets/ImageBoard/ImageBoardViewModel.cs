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
  
    [ObservableProperty] private bool _isExpanded = true;

    [RelayCommand]
    private void Paste() => PasteFromClipboard();



    private readonly WebImageService _web;
    private readonly AppSettings _settings;
    private readonly JsonStore<ImageBoardData> _store = new("images.json");
    private readonly ImageBoardData _data;
    private readonly ToastService _toast;

    public ObservableCollection<ImageBoardItemViewModel> Items { get; } = new();

    public ImageBoardViewModel(WebImageService web, AppSettings settings, ToastService toast)
    {
        _web = web;
        _settings = settings;
        _toast = toast;
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
            _toast.Success("复制图片");
        }
        catch (Exception ex) {
            _toast.Success("复制图片失败");
            Logger.Error("复制图片失败", ex); 
        }
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

    /// <summary>把本地图片文件复制进看板目录并加入看板。</summary>
    public void AddLocalFiles(string[] files)
    {
        int seq = 0;
        foreach (var src in files)
        {
            try
            {
                _toast.Success("本地图片进看板");
                if (!File.Exists(src)) continue;
                string ext = Path.GetExtension(src);
                string fileName = $"img_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{seq++}{ext}";
                string dest = Path.Combine(Paths.BoardImagesDir, fileName);
                File.Copy(src, dest, overwrite: false);
                AddModel(new ImageBoardItem { FileName = fileName, CreatedAt = Now() });
            }
            catch (Exception ex) {
                _toast.Success("本地图片加入看板失败");
                Logger.Error($"本地图片加入看板失败: {src}", ex);
            }
        }
    }



    public async Task AddFromUrlAsync(string url)
    {
        var model = new ImageBoardItem { CreatedAt = Now() };
        var vm = new ImageBoardItemViewModel(model) { State = ImageLoadState.Loading };
        Items.Add(vm);
        OnPropertyChanged(nameof(HasContent));

        try
        {
            _toast.Success("网络图片添加到看板");
            var bmp = await _web.DownloadBitmapAsync(url);
            var fileName = SaveBitmap(bmp);
            if (fileName == null) throw new Exception("保存失败");
            _data.Items.Add(model);
            vm.SetReady(fileName);
            Save();
        }
        catch (Exception ex)
        {
            _toast.Success("网络图片添加失败");
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
        else if (ctx.HasImageFiles)
            yield return new DropSlot { Title = "🖼 图片到看板", OnDrop = c => AddLocalFiles(c.ImageFiles) };
        else if (ctx.HasImageUrl && _settings.EnableImageDownload)
            yield return new DropSlot
            {
                Title = "🖼 网络图片到看板",
                OnDrop = async c => await AddFromUrlAsync(c.ImageUrl!)
            };
    }


    /// <summary>把剪贴板里的图片粘贴进看板。</summary>
    /// <summary>把剪贴板里的图片粘贴进看板（带占位符 + 重试 + 异常兜底）。</summary>
    public void PasteFromClipboard()
    {
        _toast.Success("尝试把剪贴板内容粘贴进看板");
        // 先取出剪贴板内容（剪贴板访问可能因被占用抛 COMException，做重试）
        BitmapSource? clipImage = TryGetClipboardImage();
        if (clipImage != null)
        {
            AddBitmapWithPlaceholder(clipImage);
            return;
        }

        var files = TryGetClipboardImageFiles();
        if (files.Length > 0)
        { AddLocalFiles(files); }
        else
        { 
            Logger.Warn("剪贴板中没有可用的图片");
            _toast.Success("剪贴板中没有可用的图片");
        }
    }

    private static BitmapSource? TryGetClipboardImage()
    {
        for (int i = 0; i < 5; i++)
        {
            try
            {
                if (Clipboard.ContainsImage())
                {
                    var img = Clipboard.GetImage();
                    if (img == null) return null;
                    // GetImage 返回的 InteropBitmap 在跨线程/保存时易出问题，转成稳定的可冻结副本
                    var stable = new System.Windows.Media.Imaging.WriteableBitmap(img);
                    stable.Freeze();
                    return stable;
                }
                return null;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                System.Threading.Thread.Sleep(60);   // 剪贴板被占用，稍后重试
            }
            catch (Exception ex)
            {
                Logger.Error("读取剪贴板图片失败", ex);
                return null;
            }
        }
        Logger.Warn("剪贴板被占用，多次重试后仍无法读取图片");
        return null;
    }

    private static string[] TryGetClipboardImageFiles()
    {
        try
        {
            if (!Clipboard.ContainsFileDropList()) return Array.Empty<string>();
            var list = Clipboard.GetFileDropList();
            var arr = new string[list.Count];
            list.CopyTo(arr, 0);
            return arr;
        }
        catch (Exception ex) { Logger.Error("读取剪贴板文件失败", ex); return Array.Empty<string>(); }
    }

    /// <summary>插占位项→后台保存位图→就绪或失败。</summary>
    private async void AddBitmapWithPlaceholder(BitmapSource bmp)
    {
        var model = new ImageBoardItem { CreatedAt = Now() };
        var vm = new ImageBoardItemViewModel(model) { State = ImageLoadState.Loading };
        Items.Add(vm);
        OnPropertyChanged(nameof(HasContent));
        _toast.Success("正在保存图片…");
        try
        {
            // 保存是磁盘 IO，放后台线程；bmp 已 Freeze，可跨线程使用
            string? fileName = await System.Threading.Tasks.Task.Run(() => SaveBitmap(bmp));
            if (fileName == null) throw new Exception("保存失败");
            _data.Items.Add(model);
            vm.SetReady(fileName);
            Save();
            _toast.Success("已添加到看板");
        }
        catch (Exception ex)
        {
            Logger.Error("粘贴图片保存失败", ex);
            vm.State = ImageLoadState.Failed;
            _toast.Error("图片保存失败");
        }
    }



}
