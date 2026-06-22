using CommunityToolkit.Mvvm.ComponentModel;
using JTool.Services;

using System.Windows.Media.Imaging;

namespace JTool.Widgets.ShortcutGrid;

public sealed partial class ShortcutItemViewModel : ObservableObject
{


    private readonly IconService _icons;
    public ShortcutItem Model { get; }

    public ShortcutItemViewModel(ShortcutItem model, IconService icons)
    {
        Model = model;
        _icons = icons;
    }

    public string Name => Model.Name;
    public string Path => Model.Path;
    public BitmapSource? Icon => _icons.GetIcon(Model.Path, large: true);
}
