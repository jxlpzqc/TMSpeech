using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Media;
using Avalonia.Threading;
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

    private enum UpdateMode
    {
        ViewToBoth,
        PluginLayerToViewToValue,
        ValueToViewToPluginLayer,
    }

    private UpdateMode _updateMode = UpdateMode.ViewToBoth;

    private void UpdateValueAndNotify()
    {
        Value = ConfigEditor.GenerateConfig();
    }

    private void LoadValuesToView()
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
                    case ComboBox cb:
                        cb.SelectedValue = value;
                        break;
                }
            }
        }
    }

    // generate controls and events
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
                    if (_updateMode != UpdateMode.ViewToBoth) return;

                    ConfigEditor.SetValue(formItem.Key, tb.Text);
                    UpdateValueAndNotify();
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
                    if (_updateMode != UpdateMode.ViewToBoth) return;
                    
                    ConfigEditor.SetValue(formItem.Key, fp.Text);
                    UpdateValueAndNotify();
                };
                control = fp;
            }
            else if (formItem is PluginConfigFormItemOption optionFormItem)
            {
                var cb = new ComboBox()
                {
                    Tag = optionFormItem.Key,
                    ItemsSource = optionFormItem.Options.ToList(),
                    SelectedValueBinding = new Binding("Key"),
                    ItemTemplate = new FuncDataTemplate<KeyValuePair<object, string>>((v, namescope) => new TextBlock()
                        {
                            [!TextBlock.TextProperty] = new Binding("Value"),
                        }
                    )
                };

                cb.SelectionChanged += (_, _) =>
                {
                    if (_updateMode != UpdateMode.ViewToBoth) return;
                    
                    ConfigEditor.SetValue(formItem.Key, optionFormItem.Options.Keys.ToList()[cb.SelectedIndex]);
                    UpdateValueAndNotify();
                };
                control = cb;
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
                oldConfig.FormItemsUpdated -= OnPluginLayerConfigFormItemsUpdated;
            }

            GenerateControls();
            if (change.NewValue is IPluginConfigEditor newConfig)
            {
                OnPluginLayerConfigValueUpdated(this, null);
                newConfig.ValueUpdated += OnPluginLayerConfigValueUpdated;
                newConfig.FormItemsUpdated += OnPluginLayerConfigFormItemsUpdated;
            }
        }
        else if (change.Property == ValueProperty)
        {
            if (_updateMode != UpdateMode.ViewToBoth) return;
            _updateMode = UpdateMode.ValueToViewToPluginLayer;
            ConfigEditor?.LoadConfigString(change.GetNewValue<string>());
            LoadValuesToView();
            _updateMode = UpdateMode.ViewToBoth;
        }
    }


    private void OnPluginLayerConfigFormItemsUpdated(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            if (_updateMode != UpdateMode.ViewToBoth) return;
            _updateMode = UpdateMode.PluginLayerToViewToValue;
            GenerateControls();
            LoadValuesToView();
            _updateMode = UpdateMode.ViewToBoth;
        });
    }

    private void OnPluginLayerConfigValueUpdated(object? sender, EventArgs e)
    {
        if (_updateMode != UpdateMode.ViewToBoth) return;
        _updateMode = UpdateMode.PluginLayerToViewToValue;
        UpdateValueAndNotify();
        LoadValuesToView();
        _updateMode = UpdateMode.ViewToBoth;
    }
}