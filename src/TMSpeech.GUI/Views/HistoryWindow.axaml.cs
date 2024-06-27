using Avalonia.Controls;
using TMSpeech.GUI.ViewModels;

namespace TMSpeech.GUI.Views;

public partial class HistoryWindow : Window
{
    public HistoryWindow(MainViewModel model)
    {
        InitializeComponent();
        DataContext = model;
    }
}
