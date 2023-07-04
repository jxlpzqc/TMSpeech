using NAudio.Wave;
using SherpaOnnx;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TMSpeech.GUI
{
    class SpeechCore : ISpecchRecognition
    {
        public IList<TextInfo> AllTexts { get; set; } = new List<TextInfo>();
        public string CurrentText { get; set; }
        public event EventHandler<SpeechEventArgs> TextChanged;
        public event EventHandler<EventArgs> UpdateList;

        string Encoder;
        string Decoder;
        string Joiner;
        string Tokens;
        string Savefile;

        // 如果saveFile为空或null，则表示不保存到文件。
        public SpeechCore(string encoder, string decoder, string joiner, string tokens, string savefile)
        {
            this.Encoder = encoder;
            this.Decoder = decoder;
            this.Joiner = joiner;
            this.Tokens = tokens;
            this.Savefile = savefile;
        }

        public void Clear()
        {
            AllTexts.Clear();
            CurrentText = "";
        }

        private OnlineRecognizer recognizer;

        private WasapiLoopbackCapture capture;

        // 如果saveFile为空或null，则表示不保存到文件。
        public void Run(string encoder, string decoder, string joiner, string tokens, string savefile)
        {
            disposed = false;
            OnlineRecognizerConfig config = new OnlineRecognizerConfig();
            config.FeatConfig.SampleRate = 16000;
            config.FeatConfig.FeatureDim = 80;
            config.TransducerModelConfig.Encoder = encoder;
            config.TransducerModelConfig.Decoder = decoder;
            config.TransducerModelConfig.Joiner = joiner;
            config.TransducerModelConfig.Tokens = tokens;
            config.TransducerModelConfig.NumThreads = 1;
            config.TransducerModelConfig.Debug = 1;

            config.DecodingMethod = "greedy_search";
            config.EnableEndpoint = 1;

            config.Rule1MinTrailingSilence = 2.4f;
            config.Rule2MinTrailingSilence = 1.2f;
            config.Rule3MinUtteranceLength = 300;

            recognizer = new OnlineRecognizer(config);

            OnlineStream s = recognizer.CreateStream();

            int inputSampleRate = config.FeatConfig.SampleRate;
            capture = new WasapiLoopbackCapture();
            capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(inputSampleRate, 1);
            capture.DataAvailable += (_, a) =>
            {
                var buffer = new float[a.BytesRecorded / 4];
                Buffer.BlockCopy(a.Buffer, 0, buffer, 0, a.BytesRecorded);
                s.AcceptWaveform(inputSampleRate, buffer);
            };
            capture.RecordingStopped += (_, a) =>
            {
                s.InputFinished();
                capture.Dispose();
            };

            capture.StartRecording();

            while (!disposed)
            {
                while (recognizer.IsReady(s))
                {
                    recognizer.Decode(s);
                }
                var is_endpoint = recognizer.IsEndpoint(s);
                var text = recognizer.GetResult(s).Text;

                if (!string.IsNullOrEmpty(text))
                {
                    var item = new TextInfo(text);
                    TextChanged?.Invoke(this, new SpeechEventArgs()
                    {
                        Text = item,
                    });
                    CurrentText = text;

                    if (is_endpoint || text.Length >= 80)
                    {
                        AllTexts.Add(item);
                        if (!string.IsNullOrEmpty(savefile))
                        {
                            try
                            {
                                File.AppendAllText(savefile, string.Format("{0:T}: {1}\n", item.Time, item.Text));
                            }
                            catch { }
                        }

                        recognizer.Reset(s);
                        UpdateList?.Invoke(this, EventArgs.Empty);
                    }
                }
                Thread.Sleep(20);
            }

            if (capture != null)
            {
                capture.StopRecording();
                capture.Dispose();
            }
            if (recognizer != null)
                recognizer.Dispose();
            capture = null;
            recognizer = null;
        }

        private bool disposed = false;

        public void Dispose()
        {
            disposed = true;
        }

        public void SetTextChangedHandler(EventHandler<SpeechEventArgs> handler)
        {
            this.TextChanged += handler;
        }

        public void SetUpdateListHandler(EventHandler<EventArgs> handler)
        {
            this.UpdateList += handler;
        }

        public void Run()
        {
            Run(Encoder, Decoder, Joiner, Tokens, Savefile);
        }

        public IList<TextInfo> GetAllTexts()
        {
            return AllTexts;
        }
    }
}
