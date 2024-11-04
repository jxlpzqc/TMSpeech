namespace TMSpeech.Core.Plugins;

public interface IRunable
{
    void Start();
    void Stop();
    event EventHandler<Exception> ExceptionOccured;
}