namespace TMSpeech.Core.Services.Notification;

public class NotificationManager
{
    private List<INotificationService> _services;
    private NotificationType _level;

    public void RegistService(INotificationService service)
    {
        _services.Add(service);
    }

    public void UnregistNotificationService(INotificationService service)
    {
        _services.Remove(service);
    }

    public void Notify(string content, string? title, NotificationType type = NotificationType.Info)
    {
        if (type > _level)
        {
            foreach (var service in _services)
            {
                service.Notify(content, title, type);
            }
        }
    }

    public void SetNotifyLevel(NotificationType level)
    {
        _level = level;
    }

    private NotificationManager()
    {
    }

    private static NotificationManager? _instance;

    public static NotificationManager Instance
    {
        get { return _instance ??= new NotificationManager(); }
    }
}