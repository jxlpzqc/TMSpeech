using System;
using Avalonia;
using Avalonia.ReactiveUI;
using DesktopNotifications;
using MsBox.Avalonia;
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
        try
        {
            Initializer.InitialzeServices();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception e)
        {
            System.Diagnostics.Debug.WriteLine($"主线程 unhandled exception: {e.Message}");
            // TODO 当前窗口会卡死，等待10秒展示报错后退出。如何防止卡死？
            MessageBoxManager.GetMessageBoxStandard("严重错误", $"未捕获的异常：\n{e.Message}\n{e.StackTrace}\n请将问题截图提交到Github Issue，10s后应用将退出").ShowAsync().Wait(10000);
        }
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