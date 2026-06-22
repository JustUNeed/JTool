using JTool.Hosting;

namespace JTool.Widgets.ImageBoard;

public partial class ImageBoardControl : System.Windows.Controls.UserControl, IPanelWidget
{
    private readonly ImageBoardViewModel _vm;

    public ImageBoardControl(ImageBoardViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    public string Title => "图片看板";
    public bool HasContent => _vm.HasContent;
    public object View => this;
}
