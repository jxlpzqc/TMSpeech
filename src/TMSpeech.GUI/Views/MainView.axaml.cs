using Avalonia;
using Avalonia.Controls;

namespace TMSpeech.GUI.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<MainView, string>(
        "Text");

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}