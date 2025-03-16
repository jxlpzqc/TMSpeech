using MsBox.Avalonia;
using TMSpeech.GUI.ViewModels;
using System.Windows.Input;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using ReactiveUI;
using TMSpeech.Core.Plugins;

namespace TMSpeech.GUI.Views;

public partial class HistoryWindow : ReactiveWindow<MainViewModel>
{
    public HistoryWindow(MainViewModel model)
    {
        this.ViewModel = model;
        InitializeComponent();
        this.WhenActivated(d =>
        {
            void OnHistoryTextsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
            {
                Dispatcher.UIThread.Invoke(() => { this.scrollViewer.ScrollToEnd(); });
            }

            ViewModel.HistoryTexts.CollectionChanged += OnHistoryTextsOnCollectionChanged;
            Disposable.Create(() => { ViewModel.HistoryTexts.CollectionChanged -= OnHistoryTextsOnCollectionChanged; })
                .DisposeWith(d);
        });
    }
}