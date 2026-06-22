using JTool.Hosting;
using System.ComponentModel;

namespace JTool.Widgets.ShortcutGrid;

public partial class ShortcutGridControl : System.Windows.Controls.UserControl, IPanelWidget
{
    private readonly ShortcutGridViewModel _vm;

    public ShortcutGridControl(ShortcutGridViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        _vm.PropertyChanged += (_, e) => PropertyChanged?.Invoke(this, e);
    }

    public string Title => "快捷";
    public bool HasContent => _vm.HasContent;
    public object View => this;

    public bool IsExpanded
    {
        get => _vm.IsExpanded;
        set => _vm.IsExpanded = value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}