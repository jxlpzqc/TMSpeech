using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Reactive.Concurrency;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using TMSpeech.Core;
using TMSpeech.GUI.ViewModels;
using TMSpeech.GUI.Views;

namespace TMSpeech.GUI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        ConfigManagerFactory.Init(DefaultConfig.GenerateConfig());
    }

    private MainWindow _mainWindow;
    public MainWindow MainWindow => _mainWindow;
    
    public void UpdateTrayMenu(bool locked = false)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            (TrayIcon.GetIcons(this).First().Menu as Controls.TrayMenu).UpdateItems();
        }
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
            _mainWindow = new MainWindow();
            desktop.MainWindow = _mainWindow;

            try
            {
                var savedLocationObj = ConfigManagerFactory.Instance.Get<JsonElement>(GeneralConfigTypes.MainWindowLocation);
                if (savedLocationObj.ValueKind == JsonValueKind.Array)
                {
                    var savedLocation = savedLocationObj.Deserialize<int[]>();
                    if (savedLocation != null && savedLocation.Length == 4)
                    {
                        // Validate values
                        if (savedLocation[2] > 0 && savedLocation[3] > 0)
                        {
                            _mainWindow.Width = savedLocation[2];
                            _mainWindow.Height = savedLocation[3];
                            _mainWindow.Position = new(savedLocation[0], savedLocation[1]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Silently fall back to default window position - no need to notify user
                // This is a minor issue that doesn't affect functionality
                System.Diagnostics.Debug.WriteLine($"Failed to restore window position: {ex.Message}");
            }
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            // TODO: Implement single view platform initialization
            // singleViewPlatform.MainView = new CaptionView();
        }

        base.OnFrameworkInitializationCompleted();

        if (!Design.IsDesignMode)
        {
            try
            {
                Core.Plugins.PluginManagerFactory.GetInstance().LoadPlugins();
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    await MessageBoxManager.GetMessageBoxStandard(
                        "插件加载失败",
                        $"无法加载插件，应用程序可能无法正常工作。\n\n错误详情：{ex.Message}",
                        ButtonEnum.Ok,
                        Icon.Error
                    ).ShowAsync();
                });
            }

            // Run recognizer if config is set.
            try
            {
                if (ConfigManagerFactory.Instance.Get<bool>(GeneralConfigTypes.StartOnLaunch))
                {
                    Dispatcher.UIThread.Post(() => { _mainWindow.ViewModel.PlayCommand.Execute(); });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    await MessageBoxManager.GetMessageBoxStandard(
                        "自动启动失败",
                        $"无法自动启动识别器。\n\n错误详情：{ex.Message}",
                        ButtonEnum.Ok,
                        Icon.Warning
                    ).ShowAsync();
                });
            }
        }
    }
}