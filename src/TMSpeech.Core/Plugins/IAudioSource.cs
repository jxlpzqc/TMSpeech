using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMSpeech.Core.Plugins
{
    public enum SourceStatus
    {
        Unavailable, Ready, Busy
    }

    public interface IAudioSource : IPlugin, IRunable
    {

        event EventHandler<SourceStatus> StatusChanged;
        event EventHandler<byte[]> DataAvailable;
    }
}
