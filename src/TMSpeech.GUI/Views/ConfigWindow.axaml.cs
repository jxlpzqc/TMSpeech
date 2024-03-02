using System;
using System.Reactive.Disposables;
using Avalonia.ReactiveUI;
using ReactiveUI;
using TMSpeech.GUI.ViewModels;

namespace TMSpeech.GUI.Views
{
    public partial class ConfigWindow : ReactiveWindow<ConfigViewModel>
    {
        public ConfigWindow()
        {
            InitializeComponent();
            ViewModel = new ConfigViewModel();


            this.WhenActivated(d =>
            {
                ViewModel.WindowNeedClose
                    .Subscribe(_ => { this.Close(); })
                    .DisposeWith(d);
            });
        }
    }
}