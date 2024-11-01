using TMSpeech.Core.Services.Notification;

namespace TMSpeech.GUI.Services;

static public class Initializer
{
    static public void InitialzeServices()
    {
        NotificationManager.Instance.RegistService(new NotificationService());
    }
}