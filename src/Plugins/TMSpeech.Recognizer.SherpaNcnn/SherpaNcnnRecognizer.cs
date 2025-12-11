using SherpaNcnn;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using TMSpeech.Core.Plugins;
using TMSpeech.Core.Services.Resource;
namespace TMSpeech.Recognizer.SherpaNcnn
{
    public class SherpaNcnnRecognizer : IRecognizer
    {
        public string GUID => "94C23641-CBE0-42B6-9654-82DA42D519F3";
        public string Name => "Sherpa-Ncnn离线识别器";
        public string Description => "可以调用GPU的识别器";
        public string Version => "0.0.1";
        public string SupportVersion => "any";
        public string Author => "Built-in";
        public string Url => "";
        public string License => "MIT License";
        public string Note => "";
        
        public event EventHandler<SpeechEventArgs> TextChanged;
        public event EventHandler<SpeechEventArgs> SentenceDone;
        public event EventHandler<Exception>? ExceptionOccured;
        private CancellationTokenSource _cts;
        private OnlineRecognizerConfig config;
        private Thread thread;

        private OnlineRecognizer recognizer;
        
        private OnlineStream stream;

        public bool Available => true;
        private SherpaNcnnConfig _userConfig = new SherpaNcnnConfig();


        private void Run(CancellationToken cancellationToken)
        {
            config = new OnlineRecognizerConfig();
            config.FeatConfig.SampleRate = 16000;
            config.FeatConfig.FeatureDim = 80;
            config.ModelConfig.Tokens = _userConfig.Tokens;
            config.ModelConfig.EncoderParam = _userConfig.EncoderParam;
            config.ModelConfig.EncoderBin = _userConfig.EncoderBin;

            config.ModelConfig.DecoderParam = _userConfig.DecoderParam;
            config.ModelConfig.DecoderBin = _userConfig.DecoderBin;

            config.ModelConfig.JoinerParam = _userConfig.JoinerParam;
            config.ModelConfig.JoinerBin = _userConfig.JoinerBin;

            foreach (string path in new[] { _userConfig.Tokens, _userConfig.EncoderParam, _userConfig.EncoderBin, _userConfig.DecoderParam,
            _userConfig.DecoderBin,_userConfig.JoinerParam, _userConfig.JoinerBin})
            {
                if (!File.Exists(path))
                {
                    throw new InvalidOperationException("Cannot find model file: " + path +
                                                        "\n Current working directory: " +
                                                        Directory.GetCurrentDirectory());
                }
            }
            config.ModelConfig.UseVulkanCompute = 1;
            config.ModelConfig.NumThreads = 1;


            config.DecoderConfig.DecodingMethod = "greedy_search";


            config.DecoderConfig.NumActivePaths = 4;
            config.EnableEndpoint = 1;
            config.Rule1MinTrailingSilence = 2.4F;
            config.Rule2MinTrailingSilence = 1.2F;
            config.Rule3MinUtteranceLength = 20.0F;

            recognizer = new OnlineRecognizer(config);
            stream = recognizer.CreateStream();

            while (!cancellationToken.IsCancellationRequested)
            {
                while (recognizer.IsReady(stream) && !cancellationToken.IsCancellationRequested )
                {
                    recognizer.Decode(stream);
                }
                if (cancellationToken.IsCancellationRequested)
                    break;
                var is_endpoint = recognizer.IsEndpoint(stream);
                var text = recognizer.GetResult(stream).Text;

                if (!string.IsNullOrEmpty(text))
                {
                    var item = new TextInfo(text);
                    TextChanged?.Invoke(this, new SpeechEventArgs()
                    {
                        Text = item,
                    });

                    if (is_endpoint || text.Length >= 80)
                    {
                        SentenceDone?.Invoke(this, new SpeechEventArgs()
                        {
                            Text = item,
                        });
                        recognizer.Reset(stream);
                    }
                }

                Thread.Sleep(20);
            }
        }

        public void Init()
        {
            Debug.WriteLine("SherpaNcnnRecognizer Init");
        }

        public void Destroy()
        {
        }

        void IRecognizer.Feed(byte[] data)
        {
            var buffer = MemoryMarshal.Cast<byte, float>(data);
            stream?.AcceptWaveform(config.FeatConfig.SampleRate, buffer.ToArray());
        }

        IPluginConfigEditor IPlugin.CreateConfigEditor() => new SherpaNcnnConfigEditor();

        void IPlugin.LoadConfig(string config)
        {
            if (config != null && config.Length != 0)
            {
                _userConfig = JsonSerializer.Deserialize<SherpaNcnnConfig>(config);
            }
        }

        void IRunable.Start()
        {

            if (thread != null)
            {
                Debug.WriteLine($"[START] Found existing thread. Alive: {thread.IsAlive}, State: {thread.ThreadState}");
                if (thread.IsAlive)
                {
                    throw new InvalidOperationException("The recognizer is already running.");
                }
                else
                {
                    Debug.WriteLine("[START] Cleaning up dead thread reference...");
                    thread = null;
                }
            }

            _cts = new CancellationTokenSource();
            thread = new Thread(() =>
            {
                try
                {
                    Run(_cts.Token);
                }
                catch (Exception e)
                {
                    Trace.TraceError("{0:HH:mm:ss.fff} Exception {1}", DateTime.Now, e);
                    ExceptionOccured?.Invoke(this, e);
                }
            });
            thread.IsBackground = true;
            thread.Name = "SherpaNcnnRecognizer";
            thread.Start();
        }

        void IRunable.Stop()
        {
            stream?.InputFinished();
            _cts?.Cancel();
            thread?.Join();

            stream?.Dispose();
            recognizer?.Dispose();
            recognizer = null;
            stream = null;
            thread = null;
        }
    }
}
