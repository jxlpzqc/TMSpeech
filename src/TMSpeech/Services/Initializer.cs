using TMSpeech.Core.Services.Notification;

namespace TMSpeech.Services;

static public class Initializer
{
    static public void InitialzeServices()
    {
        NotificationManager.Instance.RegistService(new NotificationService());
    }
}