using System;
using System.IO;
using System.Windows.Media.Imaging;
using JTool.Models;

namespace JTool.ViewModels;

public class BoardItemViewModel : ObservableObject
{
    public BoardItem Model { get; }

    public BoardItemViewModel(BoardItem model) => Model = model;

    public BoardItemType Type => Model.Type;
    public bool IsText => Model.Type == BoardItemType.Text;
    public bool IsImage => Model.Type == BoardItemType.Image;

    public string Text => Model.Text;

    // 单行预览（换行转空格，长度交给 UI 的 TextTrimming）
    public string Preview => (Model.Text ?? "").Replace("\r", " ").Replace("\n", " ");

    public string ImagePath => Model.ImagePath;

    // ===== 加载态（占位符用）=====
    private BoardLoadState _loadState = BoardLoadState.Ready;
    public BoardLoadState LoadState
    {
        get => _loadState;
        set
        {
            if (SetProperty(ref _loadState, value))
            {
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(IsFailed));
                OnPropertyChanged(nameof(IsImageReady));
            }
        }
    }
    public bool IsLoading => _loadState == BoardLoadState.Loading;
    public bool IsFailed => _loadState == BoardLoadState.Failed;
    /// <summary>图片已就绪可显示（用于控制真图 Image 的显隐）。</summary>
    public bool IsImageReady => IsImage && _loadState == BoardLoadState.Ready;

    private BitmapImage? _thumbnail;
    public BitmapImage? Thumbnail
    {
        get
        {
            if (_thumbnail == null && IsImage && File.Exists(ImagePath))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = 200;
                bmp.UriSource = new Uri(ImagePath);
                bmp.EndInit();
                bmp.Freeze();
                _thumbnail = bmp;
            }
            return _thumbnail;
        }
    }

    /// <summary>下载完成后写入真实路径并刷新缩略图。</summary>
    public void SetImageReady(string absolutePath)
    {
        Model.ImagePath = absolutePath;
        _thumbnail = null;                 // 清缓存，下次 get 重新解码
        LoadState = BoardLoadState.Ready;
        OnPropertyChanged(nameof(ImagePath));
        OnPropertyChanged(nameof(Thumbnail));
    }
}
