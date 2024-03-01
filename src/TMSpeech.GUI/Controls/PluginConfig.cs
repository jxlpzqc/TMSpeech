using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TMSpeech.Core.Plugins;

namespace TMSpeech.GUI.Controls;

public class PluginConfig : UserControl
{
    private readonly Grid _container;

    public PluginConfig()
    {
        _container = new AutoGrid()
        {
            RowCount = 100,
            ColumnDefinitions = new ColumnDefinitions("100,*"),
        };
        this.Content = _container;
    }

    public static readonly StyledProperty<IPluginConfiguration?> ConfigProperty =
        AvaloniaProperty.Register<PluginConfig, IPluginConfiguration?>(
            nameof(IPluginConfiguration));

    private void UpdateValues()
    {
        if (Config == null) return;
        var values = Config.GetAll();
        foreach (var control in _container.Children.OfType<Control>())
        {
            if (control.Tag is string key)
            {
                if (!values.TryGetValue(key, out var value)) continue;
                switch (control)
                {
                    case TextBox tb:
                        tb.Text = value;
                        break;
                    case FilePicker fp:
                        fp.Text = value;
                        break;
                }
            }
        }
    }

    private void GenerateControls()
    {
        _container.Children.Clear();
        if (Config == null) return;
        foreach (var meta in Config.ListMeta())
        {
            var label = new Label()
            {
                Content = meta.Name,
            };
            _container.Children.Add(label);
            Control control;
            if (meta.Type == PluginConfigurationMeta.MetaType.Text)
            {
                var tb = new TextBox()
                {
                    Text = meta.DefaultValue,
                    Tag = meta.Key
                };
                tb.TextChanged += (_, _) => { Config.Set(meta.Name, tb.Text); };
                control = tb;
            }
            else if (meta.Type == PluginConfigurationMeta.MetaType.File ||
                     meta.Type == PluginConfigurationMeta.MetaType.Folder)
            {
                var fp = new FilePicker()
                {
                    Tag = meta.Key,
                    Text = meta.DefaultValue,
                    Type = meta.Type == PluginConfigurationMeta.MetaType.File
                        ? FilePickerType.File
                        : FilePickerType.Folder,
                };
                fp.FileChanged += (_, _) => { Config.Set(meta.Name, fp.Text); };
                control = fp;
            }
            else
            {
                control = new Label()
                {
                    Content = "Not supported",
                    Foreground = Brushes.Red,
                };
            }

            _container.Children.Add(control);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ConfigProperty)
        {
            if (change.OldValue is IPluginConfiguration oldConfig)
            {
                oldConfig.ValueUpdated -= ConfigValueUpdated;
            }


            GenerateControls();
            if (change.NewValue is IPluginConfiguration newConfig)
            {
                UpdateValues();
                newConfig.ValueUpdated += ConfigValueUpdated;
            }
        }
    }

    private void ConfigValueUpdated(object? sender, EventArgs e)
    {
        UpdateValues();
    }

    public IPluginConfiguration? Config
    {
        get => GetValue(ConfigProperty);
        set => SetValue(ConfigProperty, value);
    }
}