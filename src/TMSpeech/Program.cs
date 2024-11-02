using System;
using Avalonia;
using Avalonia.ReactiveUI;
using DesktopNotifications;
using TMSpeech.Services;

namespace TMSpeech.GUI.Desktop;

class Program
{
    internal static INotificationManager NotificationManager = null!;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Initializer.InitialzeServices();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .SetupDesktopNotifications(out NotificationManager!)
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
}