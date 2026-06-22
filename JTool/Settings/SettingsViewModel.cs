using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace JTool.Settings;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _service;
    private readonly AppSettings _s;

    public SettingsViewModel(SettingsService service)
    {
        _service = service;
        _s = service.Current;
    }

    public bool AutoStart
    {
        get => _s.AutoStart;
        set { if (_s.AutoStart != value) { _s.AutoStart = value; OnPropertyChanged(); } }
    }

    public bool Topmost
    {
        get => _s.Topmost;
        set { if (_s.Topmost != value) { _s.Topmost = value; OnPropertyChanged(); } }
    }

    public double BallSize
    {
        get => _s.BallSize;
        set { if (_s.BallSize != value) { _s.BallSize = value; OnPropertyChanged(); } }
    }

    public bool EnableImageDownload
    {
        get => _s.EnableImageDownload;
        set { if (_s.EnableImageDownload != value) { _s.EnableImageDownload = value; OnPropertyChanged(); } }
    }

    [RelayCommand]
    private void Save() => _service.Save();
}
