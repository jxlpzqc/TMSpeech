using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace TMSpeech.GUI.Controls;

public enum FilePickerType
{
    File,
    Folder
}

public partial class FilePicker : UserControl
{
    public FilePicker()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<FilePicker, string>(nameof(Text));

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly StyledProperty<FilePickerType> TypeProperty =
        AvaloniaProperty.Register<FilePicker, FilePickerType>(nameof(Type), FilePickerType.File);

    public FilePickerType Type
    {
        get => GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    public static readonly RoutedEvent<TextChangedEventArgs> FileChangedEvent =
        RoutedEvent.Register<FilePicker, TextChangedEventArgs>(nameof(FileChanged), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs> FileChanged
    {
        add => AddHandler(FileChangedEvent, value);
        remove => RemoveHandler(FileChangedEvent, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            RaiseEvent(new TextChangedEventArgs(FileChangedEvent, this));
        }
    }

    private async void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        if (Type == FilePickerType.File)
        {
            var result = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                AllowMultiple = false,
            });
            if (result.Count > 0)
            {
                Text = result[0].Path.AbsolutePath;
            }
        }
        else if (Type == FilePickerType.Folder)
        {
            var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
            {
                AllowMultiple = false,
            });
            if (result.Count > 0)
            {
                Text = result[0].Path.AbsolutePath;
            }
        }

        if (OperatingSystem.IsWindows()) Text = Text.Replace("/", "\\");
    }
}