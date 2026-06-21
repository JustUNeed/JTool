using System.Windows;
using JTool.ViewModels;
using JTUI.Controls;

namespace JTool.Views;

public partial class SettingsWindow : JTWindow
{
    public SettingsWindow() => InitializeComponent();

    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is FloatWindowViewModel vm)
        {
            vm.AddGroupCommand.Execute(NewGroupBox.Text);
            NewGroupBox.Clear();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
