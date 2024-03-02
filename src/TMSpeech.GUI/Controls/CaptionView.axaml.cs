using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace TMSpeech.GUI.Views;

public partial class CaptionView : UserControl
{
    public CaptionView()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<Color> ShadowColorProperty = AvaloniaProperty.Register<CaptionView, Color>(
        "ShadowColor", Colors.Black);

    public Color ShadowColor
    {
        get => GetValue(ShadowColorProperty);
        set => SetValue(ShadowColorProperty, value);
    }

    public static readonly StyledProperty<int> ShadowSizeProperty = AvaloniaProperty.Register<CaptionView, int>(
        "ShadowSize", 10);

    public int ShadowSize
    {
        get => GetValue(ShadowSizeProperty);
        set => SetValue(ShadowSizeProperty, value);
    }

    // FontSize is already defined in Control
    // So use Control.FontSize instead of creating new property

    // public new static readonly StyledProperty<double> FontSizeProperty = AvaloniaProperty.Register<CaptionView, double>(
    //     "FontSize");
    //
    // public new double FontSize
    // {
    //     get => GetValue(FontSizeProperty);
    //     set => SetValue(FontSizeProperty, value);
    // }

    public static readonly StyledProperty<Color> FontColorProperty = AvaloniaProperty.Register<CaptionView, Color>(
        "FontColor", Colors.White);

    public Color FontColor
    {
        get => GetValue(FontColorProperty);
        set => SetValue(FontColorProperty, value);
    }

    public static readonly StyledProperty<TextAlignment> TextAlignProperty =
        AvaloniaProperty.Register<CaptionView, TextAlignment>(
            "TextAlign", TextAlignment.Left);

    public TextAlignment TextAlign
    {
        get => GetValue(TextAlignProperty);
        set => SetValue(TextAlignProperty, value);
    }


    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<CaptionView, string>(
        "Text");

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
}