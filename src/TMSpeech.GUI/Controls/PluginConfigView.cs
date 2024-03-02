using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TMSpeech.Core.Plugins;

namespace TMSpeech.GUI.Controls;

public class PluginConfigView : UserControl
{
    private readonly Grid _container;

    public PluginConfigView()
    {
        _container = new AutoGrid()
        {
            RowCount = 100,
            ColumnDefinitions = new ColumnDefinitions("100,*"),
        };
        this.Content = _container;
    }

    public static readonly StyledProperty<IPluginConfiguration?> ConfigProperty =
        AvaloniaProperty.Register<PluginConfigView, IPluginConfiguration?>(
            nameof(IPluginConfiguration));

    private void UpdateValueFromPluginLayer()
    {
        this.Value = Config.Save();
    }

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

    public event EventHandler<string> ValueUpdate;

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
                tb.TextChanged += (_, _) =>
                {
                    Config.Set(meta.Key, tb.Text);
                    UpdateValueFromPluginLayer();
                };
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
                fp.FileChanged += (_, _) =>
                {
                    Config.Set(meta.Key, fp.Text);
                    UpdateValueFromPluginLayer();
                };
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
                Config?.Load(Value);
                UpdateValues();
                newConfig.ValueUpdated += ConfigValueUpdated;
            }
        }
        else if (change.Property == ValueProperty)
        {
            ValueUpdate?.Invoke(this, change.NewValue as string ?? "");
            if (change.Sender != this)
            {
                Config?.Load(change.NewValue as string ?? "");
                UpdateValues();
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

    public static readonly StyledProperty<string> ValueProperty = AvaloniaProperty.Register<PluginConfigView, string>(
        "Value");


    public string Value
    {
        get => GetValue(ValueProperty);
        private set { SetValue(ValueProperty, value); }
    }
}