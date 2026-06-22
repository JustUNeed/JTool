using System.ComponentModel;
using JTool.Hosting;

namespace JTool.Widgets.ImageBoard;

public partial class ImageBoardControl
    : System.Windows.Controls.UserControl, IPanelWidget, IPasteTarget
{
    private readonly ImageBoardViewModel _vm;

    public ImageBoardControl(ImageBoardViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _vm.PropertyChanged += (_, e) => PropertyChanged?.Invoke(this, e);   // ← 补这行
    }

    public string Title => "图片看板";
    public bool HasContent => _vm.HasContent;
    public object View => this;
    public bool IsExpanded { get => _vm.IsExpanded; set => _vm.IsExpanded = value; }
    public event PropertyChangedEventHandler? PropertyChanged;

    public void PasteFromClipboard() => _vm.PasteFromClipboard();
}
