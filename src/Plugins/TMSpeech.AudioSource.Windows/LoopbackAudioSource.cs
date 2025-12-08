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
        public string GUID => "F32B7F03-7030-4960-A8DF-96377C8B5FDD";
        
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

        private WasapiLoopbackCapture capture;

        public void Start()
        {
            try
            {
                capture = new WasapiLoopbackCapture();
                capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(16000, 1);
                capture.DataAvailable += (sender, data) =>
                {
                    try
                    {
                        DataAvailable?.Invoke(sender, data.Buffer[..data.BytesRecorded]);
                    }
                    catch (Exception ex)
                    {
                        ExceptionOccured?.Invoke(this, new Exception("音频数据处理失败", ex));
                    }
                };
                capture.RecordingStopped += (sender, args) =>
                {
                    if (args.Exception != null)
                    {
                        ExceptionOccured?.Invoke(this, new Exception("录音停止异常", args.Exception));
                    }
                };

                capture.StartRecording();
                StatusChanged?.Invoke(this, SourceStatus.Ready);
            }
            catch (Exception ex)
            {
                ExceptionOccured?.Invoke(this, new Exception("启动系统内录失败", ex));
                throw;
            }
        }

        public void Stop()
        {
            try
            {
                capture?.StopRecording();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"停止录音失败: {ex.Message}");
            }

            try
            {
                capture?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"释放音频资源失败: {ex.Message}");
            }

            capture = null;
            StatusChanged?.Invoke(this, SourceStatus.Unavailable);
        }

        public event EventHandler<Exception>? ExceptionOccured;
    }
}