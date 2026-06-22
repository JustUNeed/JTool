namespace JTool.Settings;

/// <summary>全局设置，自己存 settings.json。</summary>
public sealed class AppSettings
{
    public bool AutoStart { get; set; } = false;          // 开机自启
    public bool Topmost { get; set; } = true;             // 总在最前
    public double BallSize { get; set; } = 32;            // 悬浮球大小
    public bool EnableImageDownload { get; set; } = true; // 网络图片下载开关
}
