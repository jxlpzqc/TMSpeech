using NAudio.Wave;
using SherpaOnnx;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMSpeech.GUI
{

    public class SpeechEventArgs
    {
        public TextInfo Text { get; set; }
    }

    public class TextInfo
    {
        public DateTime Time { get; set; }
        public string TimeStr => Time.ToString("T");
        public string Text { get; set; }
        public TextInfo(string text)
        {
            Time = DateTime.Now;
            Text = text;
        }
    }


    class SpeechCore
    {
        public IList<TextInfo> AllText { get; set; } = new List<TextInfo>();
        public string CurrentText { get; set; }
        public event EventHandler<SpeechEventArgs> TextChanged;
        public event EventHandler<EventArgs> UpdateList;

        public void Clear()
        {
            AllText.Clear();
            CurrentText = "";
        }

        public void Init()
        {
            OnlineRecognizerConfig config = new OnlineRecognizerConfig();
            config.FeatConfig.SampleRate = 48000;
            config.FeatConfig.FeatureDim = 80;
            config.TransducerModelConfig.Encoder = @"D:\models\encoder-epoch-99-avg-1.onnx";
            config.TransducerModelConfig.Decoder = @"D:\models\decoder-epoch-99-avg-1.onnx";
            config.TransducerModelConfig.Joiner = @"D:\models\joiner-epoch-99-avg-1.onnx";
            config.TransducerModelConfig.Tokens = @"D:\models\tokens.txt";
            config.TransducerModelConfig.NumThreads = 1;
            config.TransducerModelConfig.Debug = 1;

            config.DecodingMethod = "greedy_search";
            config.EnableEndpoint = 1;

            config.Rule1MinTrailingSilence = 2.4f;
            config.Rule2MinTrailingSilence = 1.2f;
            config.Rule3MinUtteranceLength = 300;

            OnlineRecognizer recognizer = new OnlineRecognizer(config);

            OnlineStream s = recognizer.CreateStream();

            int inputSampleRate = config.FeatConfig.SampleRate;
            var capture = new WasapiLoopbackCapture();
            capture.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(inputSampleRate, 1);
            capture.DataAvailable += (_, a) =>
            {
                var buffer = new float[a.BytesRecorded / 4];
                Buffer.BlockCopy(a.Buffer, 0, buffer, 0, a.BytesRecorded);
                s.AcceptWaveform(inputSampleRate, buffer);
            };
            capture.RecordingStopped += (_, a) =>
            {
                // TODO: dispose resources.
            };

            capture.StartRecording();

            while (true)
            {
                if (recognizer.IsReady(s))
                    recognizer.Decode(s);
                var is_endpoint = recognizer.IsEndpoint(s);
                var text = recognizer.GetResult(s).Text;
                System.Diagnostics.Trace.WriteLine(text);

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
                        AllText.Add(item);
                        recognizer.Reset(s);
                        UpdateList?.Invoke(this, EventArgs.Empty);
                    }

                }


            }
        }

    }
}
