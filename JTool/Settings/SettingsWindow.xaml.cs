using JTUI.Theming;
using System.Windows;

namespace JTool.Settings;

public partial class SettingsWindow : JTUI.Controls.JTWindow
{
    private readonly SettingsViewModel _vm;

    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void SaveClose_Click(object sender, RoutedEventArgs e)
    {
        _vm.SaveCommand.Execute(null);
        Close();
    }



    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {

        JTThemeManager.Toggle();

    }
}
