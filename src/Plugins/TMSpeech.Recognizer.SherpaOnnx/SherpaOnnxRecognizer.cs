﻿using SherpaOnnx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TMSpeech.Core.Plugins;

namespace TMSpeech.Recognizer.SherpaOnnx
{
    class SherpaOnnxRecognizer : IRecognizer
    {
        public string Name => "Sherpa-Onnx离线识别器";

        public string Description => "一款占用资源少，识别速度快的离线识别器";

        public string Version => "0.0.1";

        public string SupportVersion => "any";

        public string Author => "Built-in";

        public string Url => "";

        public string License => "MIT License";

        public string Note => "";
        public IPluginConfigEditor CreateConfigEditor() => new SherpaOnnxConfigEditor();

        private SherpaOnnxConfig _userConfig = new SherpaOnnxConfig();

        public void LoadConfig(string config)
        {
            if (config.Length != 0) {
                _userConfig = JsonSerializer.Deserialize<SherpaOnnxConfig>(config);
            }
        }

        public bool Available => true;

        public event EventHandler<SpeechEventArgs> TextChanged;
        public event EventHandler<SpeechEventArgs> SentenceDone;

        public void Feed(byte[] data)
        {
            var buffer = MemoryMarshal.Cast<byte, float>(data);
            stream?.AcceptWaveform(config.FeatConfig.SampleRate, buffer.ToArray());
        }

        private OnlineRecognizer recognizer;

        private OnlineStream stream;

        private bool stop = false;

        private Thread thread;

        private OnlineRecognizerConfig config;

        private void Run()
        {
            config = new OnlineRecognizerConfig();
            config.FeatConfig.SampleRate = 16000;
            config.FeatConfig.FeatureDim = 80;
            config.TransducerModelConfig.Encoder = _userConfig.Encoder;
            config.TransducerModelConfig.Decoder = _userConfig.Decoder;
            config.TransducerModelConfig.Joiner = _userConfig.Joiner;
            config.TransducerModelConfig.Tokens = _userConfig.Tokens;
            config.TransducerModelConfig.NumThreads = 1;
            config.TransducerModelConfig.Debug = 1;
            config.DecodingMethod = "greedy_search";
            config.EnableEndpoint = 1;
            config.Rule1MinTrailingSilence = 2.4f;
            config.Rule2MinTrailingSilence = 1.2f;
            config.Rule3MinUtteranceLength = 300;

            recognizer = new OnlineRecognizer(config);
            stream = recognizer.CreateStream();

            while (!stop)
            {
                while (recognizer.IsReady(stream))
                {
                    recognizer.Decode(stream);
                }

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

        public void Start()
        {
            foreach (string path in new[] { _userConfig.Encoder, _userConfig.Decoder, _userConfig.Joiner, _userConfig.Tokens })
            {
                if (!File.Exists(path))
                {
                    throw new InvalidOperationException("Cannot find model file: " + path +"\n Current working directory: " + Directory.GetCurrentDirectory());
                }
            }

            if (thread != null)
                throw new InvalidOperationException("The recognizer is already running.");
            stop = false;
            thread = new Thread(() =>
            {
                try
                {
                    Run();
                }
                catch (Exception e)
                {
                    Trace.TraceError("{0:HH:mm:ss.fff} Exception {1}", DateTime.Now, e);
                    // TODO: how to notify user?
                    stop = true;
                    thread = null;
                }
            });
            thread.Start();
        }

        public void Stop()
        {
            if (thread == null)
                throw new InvalidOperationException("The recognizer is not running.");

            stream?.InputFinished();
            stop = true;
            thread.Join();

            stream?.Dispose();
            recognizer?.Dispose();
            recognizer = null;
            stream = null;
            thread = null;
        }

        public void Init()
        {
            Debug.WriteLine("SherpaOnnxRecognizer Init");
        }

        public void Destroy()
        {
        }
    }
}