using System;
using System.Reactive.Disposables;
using System.Reflection;
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
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "1.0.0";
            var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;
            // 设置版本显示
            runVersion.Text = informationalVersion;
            runInternalVersion.Text = "Release Build";
        }
    }
}