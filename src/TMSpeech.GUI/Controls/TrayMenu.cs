using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using TMSpeech.Core;
using TMSpeech.GUI.Views;

namespace TMSpeech.GUI.Controls;

public class TrayMenu : NativeMenu
{
    private MainWindow _mainWindow;


    public void UpdateItems()
    {
        _mainWindow = (App.Current as App).MainWindow;
        this.Items.Clear();
        if (_mainWindow.ViewModel.IsLocked)
        {
            this.Items.Add(new NativeMenuItem
                { Header = "解锁字幕", Command = ReactiveCommand.Create(UnlockCaption) });
        }

        this.Items.Add(new NativeMenuItem { Header = "退出", Command = ReactiveCommand.Create(Exit) });
    }


    private void Exit()
    {
        // Save window location and size.
        var left = _mainWindow.Position.X;
        var top = _mainWindow.Position.Y;
        var width = (int)_mainWindow.Width;
        var height = (int)_mainWindow.Height;
        ConfigManagerFactory.Instance.Apply<List<int>>(GeneralConfigTypes.MainWindowLocation, [left, top, width, height]);
        Environment.Exit(0);
    }

    private void UnlockCaption()
    {
        _mainWindow.ViewModel.IsLocked = false;
    }
}