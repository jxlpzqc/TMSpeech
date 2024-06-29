using Avalonia.Controls;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using TMSpeech.GUI.ViewModels;
using System.Windows.Input;
using System;

namespace TMSpeech.GUI.Views;

public partial class HistoryWindow : Window
{
    public HistoryWindow()
    {
        InitializeComponent();
        listHistory.ItemsSource = new string[]
                {"cat", "camel", "cow", "chameleon", "mouse", "lion", "zebra" };
    }

    public HistoryWindow(MainViewModel model)
    {
        InitializeComponent();
        listHistory.ItemsSource = model.HistoryTexts;

        DoubelClickText = new SampleCommand();
        DoubelClickText.CanExecuteChanged += doubelClickText;
    }


    public class SampleCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            CanExecuteChanged(null, null);
        }
    }
    public ICommand DoubelClickText { get; set; }

    public void doubelClickText(object sender, EventArgs e)
    {
        string? selectedItem = (string)listHistory.SelectedItem;
        if (selectedItem != null)
        {
            string pure_text = selectedItem.Substring(selectedItem.IndexOf(']')+1);
            Clipboard.SetTextAsync(pure_text);
        }
    }
}
