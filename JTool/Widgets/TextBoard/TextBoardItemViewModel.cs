using CommunityToolkit.Mvvm.ComponentModel;


namespace JTool.Widgets.TextBoard;

public sealed partial class TextBoardItemViewModel : ObservableObject
{
    public TextBoardItem Model { get; }
    public TextBoardItemViewModel(TextBoardItem model) => Model = model;

    public string Text => Model.Text;
    public string Preview => (Model.Text ?? "").Replace("\r", " ").Replace("\n", " ");
}
