using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMSpeech.Core.Plugins;
using TMSpeech.Core.Services.Notification;

namespace TMSpeech.Core
{
    public enum JobStatus
    {
        Stopped,
        Running,
        Paused,
    }

    public static class JobControllerFactory
    {
        private static Lazy<JobController> _instance = new(() => new JobControllerImpl());
        public static JobController GetInstance() => _instance.Value;
    }

    public abstract class JobController
    {
        private JobStatus _status;

        public JobStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                StatusChanged?.Invoke(this, value);
            }
        }

        public event EventHandler<JobStatus> StatusChanged;
        public event EventHandler<SpeechEventArgs> TextChanged;
        public event EventHandler<SpeechEventArgs> SentenceDone;

        protected void OnTextChanged(SpeechEventArgs e) => TextChanged?.Invoke(this, e);
        protected void OnSentenceDone(SpeechEventArgs e) => SentenceDone?.Invoke(this, e);

        public abstract void Start();
        public abstract void Pause();
        public abstract void Stop();
    }

    public class JobControllerImpl : JobController
    {
        private readonly PluginManager _pluginManager;


        internal JobControllerImpl()
        {
            _pluginManager = PluginManagerFactory.GetInstance();
        }

        private IAudioSource? _audioSource;
        private IRecognizer? _recognizer;

        private void InitAudioSource()
        {
            if (_audioSource != null)
            {
                _audioSource.DataAvailable -= OnAudioSourceOnDataAvailable;
            }

            var configAudioSource = ConfigManagerFactory.Instance.Get<string>("audio.source");
            var config = ConfigManagerFactory.Instance.Get<string>($"plugin.{configAudioSource}.config");

            _audioSource = _pluginManager.AudioSources.FirstOrDefault(x => x.Name == configAudioSource);
            if (_audioSource != null)
            {
                _audioSource.LoadConfig(config);
                _audioSource.DataAvailable += OnAudioSourceOnDataAvailable;
            }
        }

        private void OnAudioSourceOnDataAvailable(object? _, byte[] data)
        {
            _recognizer.Feed(data);
        }

        private void InitRecognizer()
        {
            if (_recognizer != null)
            {
                _recognizer.TextChanged -= OnRecognizerOnTextChanged;
                _recognizer.SentenceDone -= OnRecognizerOnSentenceDone;
            }

            var configRecognizer = ConfigManagerFactory.Instance.Get<string>("recognizer.source");
            var config = ConfigManagerFactory.Instance.Get<string>($"plugin.{configRecognizer}.config");
            _recognizer = _pluginManager.Recognizers.FirstOrDefault(x => x.Name == configRecognizer);

            if (_recognizer != null)
            {
                _recognizer.LoadConfig(config);
                _recognizer.TextChanged += OnRecognizerOnTextChanged;
                _recognizer.SentenceDone += OnRecognizerOnSentenceDone;
            }
        }

        private void OnRecognizerOnSentenceDone(object? sender, SpeechEventArgs args)
        {
            OnSentenceDone(args);
        }

        private void OnRecognizerOnTextChanged(object? sender, SpeechEventArgs args)
        {
            OnTextChanged(args);
        }

        private void StartRecognize()
        {
            InitAudioSource();
            InitRecognizer();

            if (_audioSource == null || _recognizer == null)
            {
                Status = JobStatus.Stopped;
                NotificationManager.Instance.Notify("语音源或识别器初始化失败", "启动失败", NotificationType.Error);
                return;
            }


            try
            {
                _recognizer.Start();
            }
            catch (Exception ex)
            {
                NotificationManager.Instance.Notify($"识别器启动失败：{ex.Message}", "启动失败",
                    NotificationType.Error);
                return;
            }

            try
            {
                _audioSource.Start();
            }
            catch (Exception ex)
            {
                _recognizer.Stop();
                NotificationManager.Instance.Notify($"语音源启动失败 {ex.Message}", "启动失败",
                    NotificationType.Error);
                return;
            }

            Status = JobStatus.Running;
        }

        private void StopRecognize()
        {
            try
            {
                _audioSource?.Stop();
                _recognizer?.Stop();
            }
            catch
            {
                NotificationManager.Instance.Notify("停止失败！", "停止失败", NotificationType.Fatal);
                // TODO: exit or recover ?
                return;
            }

            _audioSource = null;
            _recognizer = null;
        }

        public override void Start()
        {
            if (Status == JobStatus.Running) return;
            StartRecognize();
        }

        public override void Pause()
        {
            if (Status == JobStatus.Running) StopRecognize();
            Status = JobStatus.Paused;
        }

        public override void Stop()
        {
            if (Status == JobStatus.Running) StopRecognize();
            Status = JobStatus.Stopped;
        }
    }
}