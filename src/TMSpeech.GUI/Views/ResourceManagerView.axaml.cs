using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using TMSpeech.Core.Services.Resource;
using TMSpeech.GUI.ViewModels;

namespace TMSpeech.GUI.Views;

public partial class ResourceManagerView : ReactiveUserControl<ResourceManagerViewModel>
{
    public ResourceManagerView()
    {
        ViewModel = new ResourceManagerViewModel();
        InitializeComponent();
    }
}