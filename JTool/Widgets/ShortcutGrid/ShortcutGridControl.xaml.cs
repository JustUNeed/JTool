using JTool.Hosting;

namespace JTool.Widgets.ShortcutGrid;

public partial class ShortcutGridControl : System.Windows.Controls.UserControl, IPanelWidget
{
    private readonly ShortcutGridViewModel _vm;

    public ShortcutGridControl(ShortcutGridViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    public string Title => "快捷";
    public bool HasContent => _vm.HasContent;
    public object View => this;
}
