using Avalonia;
using Avalonia.Controls;

namespace TMSpeech.GUI.Views;

public partial class CaptionView : UserControl
{
    public CaptionView()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<CaptionView, string>(
        "Text");

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}