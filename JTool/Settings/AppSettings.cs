using CommunityToolkit.Mvvm.ComponentModel;

namespace JTool.Settings;

/// <summary>全局设置，自己存 settings.json。</summary>
public sealed partial class AppSettings : ObservableObject
{
    [ObservableProperty] private bool _autoStart = false;
    [ObservableProperty] private bool _topmost = true;
    [ObservableProperty] private double _ballSize = 32;
    [ObservableProperty] private bool _enableImageDownload = true;

    // 悬浮球外观
    [ObservableProperty] private string _ballColor = "#FF3F51B5";
    [ObservableProperty] private string _ballText = "J";        // 空字符串 = 不显示文字
    [ObservableProperty] private double _ballCornerRadius = 8;  // 圆角半径，=BallSize/2 即正圆
    [ObservableProperty] private string _ballImagePath = "";
}
