using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using TMSpeech.Core.Services.Notification;

namespace TMSpeech.GUI.Services;

public class NotificationService : INotificationService
{
    public void Notify(string content, string? title, NotificationType type = NotificationType.Info)
    {
        // TODO: implement notification
        MessageBoxManager.GetMessageBoxStandard(title, content, ButtonEnum.Ok).ShowAsync();
    }
}