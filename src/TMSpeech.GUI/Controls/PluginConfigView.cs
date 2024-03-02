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

    public static readonly StyledProperty<IPluginConfigEditor?> ConfigEditorProperty =
        AvaloniaProperty.Register<PluginConfigView, IPluginConfigEditor?>(
            nameof(ConfigEditor));

    public IPluginConfigEditor? ConfigEditor
    {
        get => GetValue(ConfigEditorProperty);
        set => SetValue(ConfigEditorProperty, value);
    }

    public static readonly StyledProperty<string> ValueProperty = AvaloniaProperty.Register<PluginConfigView, string>(
        nameof(Value));

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private bool _isNotifying;

    private void NotifyValueUpdated()
    {
        _isNotifying = true;
        Value = ConfigEditor.GenerateConfig();
        _isNotifying = false;
    }

    private void UpdateValuesToView()
    {
        if (ConfigEditor == null) return;
        var values = ConfigEditor.GetAll();
        foreach (var control in _container.Children.OfType<Control>())
        {
            if (control.Tag is string key)
            {
                if (!values.TryGetValue(key, out var value)) continue;
                switch (control)
                {
                    case TextBox tb:
                        tb.Text = value?.ToString() ?? "";
                        break;
                    case FilePicker fp:
                        fp.Text = value?.ToString() ?? "";
                        break;
                }
            }
        }
    }

    private void GenerateControls()
    {
        _container.Children.Clear();
        if (ConfigEditor == null) return;
        foreach (var formItem in ConfigEditor.GetFormItems())
        {
            var label = new Label()
            {
                Content = formItem.Name,
            };
            _container.Children.Add(label);
            Control control;
            if (formItem is PluginConfigFormItemText)
            {
                var tb = new TextBox()
                {
                    Tag = formItem.Key
                };
                tb.TextChanged += (_, _) =>
                {
                    ConfigEditor.SetValue(formItem.Key, tb.Text);
                    NotifyValueUpdated();
                };
                control = tb;
            }
            else if (formItem is PluginConfigFormItemFile fileFormItem)
            {
                var fp = new FilePicker()
                {
                    Tag = fileFormItem.Key,
                    Type = fileFormItem.Type == PluginConfigFormItemFileType.File
                        ? FilePickerType.File
                        : FilePickerType.Folder,
                };
                fp.FileChanged += (_, _) =>
                {
                    ConfigEditor.SetValue(formItem.Key, fp.Text);
                    NotifyValueUpdated();
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
        if (change.Property == ConfigEditorProperty)
        {
            if (change.OldValue is IPluginConfigEditor oldConfig)
            {
                oldConfig.ValueUpdated -= OnPluginLayerConfigValueUpdated;
            }

            GenerateControls();
            if (change.NewValue is IPluginConfigEditor newConfig)
            {
                OnPluginLayerConfigValueUpdated(this, null);
                newConfig.ValueUpdated += OnPluginLayerConfigValueUpdated;
            }
        }
        else if (change.Property == ValueProperty)
        {
            if (_isNotifying) return;
            ConfigEditor?.LoadConfigString(change.GetNewValue<string>());
            UpdateValuesToView();
        }
    }

    private void OnPluginLayerConfigValueUpdated(object? sender, EventArgs e)
    {
        UpdateValuesToView();
        NotifyValueUpdated();
    }
}