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
}
