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
        UpdatePanelVisible();
    }

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<FilePicker, string>(nameof(Text));

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly StyledProperty<bool> ExtendedOptionsProperty = AvaloniaProperty.Register<FilePicker, bool>(
        "ExtendedOptions", false);

    public bool ExtendedOptions
    {
        get => GetValue(ExtendedOptionsProperty);
        set => SetValue(ExtendedOptionsProperty, value);
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
            if (ExtendedOptions) UpdateSelectedIndex();
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

    private const int OPTION_APPDATA = 0;
    private const int OPTION_PROGRAM = 1;
    private const int OPTION_DOCUMENTS = 2;
    private const int OPTION_DESKTOP = 3;
    private const int OPTION_CUSTOM = 4;

    private void UpdateSelectedIndex()
    {
        if (Text == "?appdata") combo.SelectedIndex = OPTION_APPDATA;
        else if (Text == "?program") combo.SelectedIndex = OPTION_PROGRAM;
        else if (Text == "?documents") combo.SelectedIndex = OPTION_DOCUMENTS;
        else if (Text == "?desktop") combo.SelectedIndex = OPTION_DESKTOP;
        else combo.SelectedIndex = OPTION_CUSTOM;
    }

    private void UpdatePanelVisible()
    {
        if (panelFileBox == null) return;
        if (!ExtendedOptions)
        {
            panelFileBox.IsVisible = true;
            return;
        }

        if (combo.SelectedIndex == OPTION_CUSTOM)
        {
            panelFileBox.IsVisible = true;
            if (Text?.StartsWith("?") == true) Text = "";
        }
        else
        {
            panelFileBox.IsVisible = false;
            switch (combo.SelectedIndex)
            {
                case OPTION_APPDATA:
                    Text = "?appdata";
                    break;
                case OPTION_PROGRAM:
                    Text = "?program";
                    break;
                case OPTION_DOCUMENTS:
                    Text = "?documents";
                    break;
                case OPTION_DESKTOP:
                    Text = "?desktop";
                    break;
            }
        }
    }

    private void SelectingItemsControl_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdatePanelVisible();
    }
}