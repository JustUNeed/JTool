using System;
using System.Windows.Media.Imaging;
using JTool.Models;
using JTool.Services;

namespace JTool.ViewModels;

public class ShortcutItemViewModel : ObservableObject
{
    private readonly IconService _iconService;
    private readonly Action? _onChanged;

    public ShortcutItem Model { get; }

    public ShortcutItemViewModel(ShortcutItem model, IconService iconService, Action? onChanged = null)
    {
        Model = model;
        _iconService = iconService;
        _onChanged = onChanged;
    }

    public string Name
    {
        get => Model.Name;
        set
        {
            if (Model.Name != value)
            {
                Model.Name = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public string Path => Model.Path;

    public string Group
    {
        get => Model.Group;
        set
        {
            if (Model.Group != value)
            {
                Model.Group = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public BitmapSource? Icon => _iconService.GetIcon(Model.Path, large: true);
}
