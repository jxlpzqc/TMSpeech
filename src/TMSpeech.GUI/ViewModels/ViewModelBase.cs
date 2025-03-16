using ReactiveUI;

namespace TMSpeech.GUI.ViewModels;

public class ViewModelBase : ReactiveObject, IActivatableViewModel
{
    public ViewModelActivator Activator { get; } = new();
}