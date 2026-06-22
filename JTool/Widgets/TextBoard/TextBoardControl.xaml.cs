using JTool.Hosting;
using System.ComponentModel;

namespace JTool.Widgets.TextBoard;

public partial class TextBoardControl : System.Windows.Controls.UserControl, IPanelWidget
{
    private readonly TextBoardViewModel _vm;

    public TextBoardControl(TextBoardViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _vm.PropertyChanged += (_, e) => PropertyChanged?.Invoke(this, e);   // ← 补这行
    }

    public string Title => "文本看板";
    public bool HasContent => _vm.HasContent;
    public object View => this;

    public bool IsExpanded
    {
        get => _vm.IsExpanded;
        set => _vm.IsExpanded = value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
