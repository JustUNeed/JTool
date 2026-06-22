using CommunityToolkit.Mvvm.ComponentModel;
using JTool.Core;

using System;
using System.IO;
using System.Windows.Media.Imaging;
using static System.Windows.Forms.AxHost;

namespace JTool.Widgets.ImageBoard;

public sealed partial class ImageBoardItemViewModel : ObservableObject
{
    public ImageBoardItem Model { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading), nameof(IsFailed), nameof(IsReady))]
    private ImageLoadState _state = ImageLoadState.Ready;

    public ImageBoardItemViewModel(ImageBoardItem model) => Model = model;

    public bool IsLoading => State == ImageLoadState.Loading;
    public bool IsFailed => State == ImageLoadState.Failed;
    public bool IsReady => State == ImageLoadState.Ready;

    public string AbsolutePath =>
        string.IsNullOrEmpty(Model.FileName) ? "" : Path.Combine(Paths.BoardImagesDir, Model.FileName);

    private BitmapImage? _thumb;
    public BitmapImage? Thumbnail
    {
        get
        {
            if (_thumb == null && IsReady && File.Exists(AbsolutePath))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 200;
                    bmp.UriSource = new Uri(AbsolutePath);
                    bmp.EndInit();
                    bmp.Freeze();
                    _thumb = bmp;
                }
                catch (Exception ex) { Logger.Error($"缩略图解码失败: {AbsolutePath}", ex); }
            }
            return _thumb;
        }
    }

    public void SetReady(string fileName)
    {
        Model.FileName = fileName;
        _thumb = null;
        State = ImageLoadState.Ready;
        OnPropertyChanged(nameof(Thumbnail));
        OnPropertyChanged(nameof(AbsolutePath));
    }
}
