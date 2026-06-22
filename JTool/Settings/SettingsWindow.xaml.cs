using System.Windows;

namespace JTool.Settings;

public partial class SettingsWindow : Window
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
}
