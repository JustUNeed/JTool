using JTool.Hosting;

namespace JTool.Widgets.TextBoard;

public partial class TextBoardControl : System.Windows.Controls.UserControl, IPanelWidget
{
    private readonly TextBoardViewModel _vm;

    public TextBoardControl(TextBoardViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    public string Title => "文本看板";
    public bool HasContent => _vm.HasContent;
    public object View => this;
}
