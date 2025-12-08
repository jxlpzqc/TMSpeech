using TMSpeech.Core.Services.Notification;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using DesktopNotifications.FreeDesktop;
using DesktopNotifications.Windows;
using System;
using DesktopNotifications;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using TMSpeech.GUI.Desktop;
using Avalonia.Threading;

namespace TMSpeech.Services;

public class NotificationService : INotificationService
{
    public void Notify(string content, string? title, NotificationType type = NotificationType.Info)
    {
        var _notification = Program.NotificationManager;
        if (_notification == null || type >= NotificationType.Error)
        {
            // macos not supported 
            Dispatcher.UIThread.Post(async () =>
            {
                await MessageBoxManager.GetMessageBoxStandard(title, content).ShowAsync();
            });
            return;
        }

        var nf = new Notification
        {
            Title = title,
            Body = content
        };
        _notification.ShowNotification(nf);
    }
}

/// <summary>
/// Extensions for <see cref="AppBuilder" />
/// </summary>
public static class AppBuilderExtensions
{
    /// <summary>
    /// Setups the <see cref="INotificationManager" /> for the current platform and
    /// binds it to the service locator (<see cref="AvaloniaLocator" />).
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static AppBuilder SetupDesktopNotifications(this AppBuilder builder, out INotificationManager? manager)
    {
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            var context = WindowsApplicationContext.FromCurrentProcess();
            manager = new WindowsNotificationManager(context);
        }
        else if (Environment.OSVersion.Platform == PlatformID.Unix)
        {
            var context = FreeDesktopApplicationContext.FromCurrentProcess();
            manager = new FreeDesktopNotificationManager(context);
        }
        else
        {
            //TODO: OSX once implemented/stable
            manager = null;
            return builder;
        }

        //TODO Any better way of doing this?
        manager.Initialize().GetAwaiter().GetResult();

        var manager_ = manager;
        builder.AfterSetup(b =>
        {
            if (b.Instance?.ApplicationLifetime is IControlledApplicationLifetime lifetime)
            {
                lifetime.Exit += (s, e) => { manager_.Dispose(); };
            }
        });

        return builder;
    }
}