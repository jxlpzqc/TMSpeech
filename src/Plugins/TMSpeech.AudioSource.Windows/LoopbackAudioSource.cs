using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TMSpeech.Core.Plugins;

namespace TMSpeech.AudioSource.Windows
{
    public class LoopbackAudioSource : IAudioSource
    {
        public string Name => "Windows 系统内录";

        public string Description => "录制系统内部声音";

        public string Version => "0.0.1";

        public string SupportVersion => "any";

        public string Author => "Built-in";

        public string Url => "";

        public string License => "MIT License";

        public string Note => "";

        public IPluginConfigEditor CreateConfigEditor()
        {
            return null;
        }

        public void LoadConfig(string config)
        {
            throw new NotImplementedException();
        }

        public bool Available => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public event EventHandler<SourceStatus> StatusChanged;
        public event EventHandler<byte[]> DataAvailable;

        public void Destroy()
        {
        }

        public void Init()
        {
        }

        WasapiLoopbackCapture capture;

        public void Start()
        {
            capture = new WasapiLoopbackCapture();
            capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(16000, 1);
            capture.DataAvailable += (sender, data) => { DataAvailable?.Invoke(sender, data.Buffer); };

            capture.StartRecording();
            StatusChanged?.Invoke(this, SourceStatus.Ready);
        }

        public void Stop()
        {
            capture.StopRecording();
            capture.Dispose();
            capture = null;
            StatusChanged?.Invoke(this, SourceStatus.Unavailable);
        }
    }
}