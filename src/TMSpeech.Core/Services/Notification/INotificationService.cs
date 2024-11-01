namespace TMSpeech.Core.Services.Notification;

public enum NotificationType
{
    Info,
    Warning,
    Error,
    Fatal
}

public interface INotificationService
{
    void Notify(string content, string? title, NotificationType type = NotificationType.Info);
}