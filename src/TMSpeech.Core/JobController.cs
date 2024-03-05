using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMSpeech.Core.Plugins;

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
            var configAudioSource = ConfigManagerFactory.Instance.Get<string>("audio.source");
            var config = ConfigManagerFactory.Instance.Get<string>($"plugin.{configAudioSource}.config");

            _audioSource = _pluginManager.AudioSources.FirstOrDefault(x => x.Name == configAudioSource);
            _audioSource?.LoadConfig(config);
        }

        private void InitRecognizer()
        {
            var configRecognizer = ConfigManagerFactory.Instance.Get<string>("recognizer.source");
            var config = ConfigManagerFactory.Instance.Get<string>($"plugin.{configRecognizer}.config");
            _recognizer = _pluginManager.Recognizers.FirstOrDefault(x => x.Name == configRecognizer);
            _recognizer?.LoadConfig(config);
        }

        private void StartRecognize()
        {
            InitAudioSource();
            InitRecognizer();

            if (_audioSource == null || _recognizer == null)
            {
                Status = JobStatus.Stopped;
                return;
            }

            // TODO: remove event handler
            _recognizer.TextChanged += (sender, args) => OnTextChanged(args);
            _recognizer.SentenceDone += (sender, args) => OnSentenceDone(args);
            _recognizer.Start();

            _audioSource.DataAvailable += (_, data) => { _recognizer.Feed(data); };
            _audioSource.Start();
        }

        private void StopRecognize()
        {
            _audioSource?.Stop();
            _recognizer?.Stop();

            _audioSource = null;
            _recognizer = null;
        }

        public override void Start()
        {
            try
            {
                StartRecognize();
                Status = JobStatus.Running;
            }
            catch (Exception e)
            {
                // TODO: notify user
            }
        }

        public override void Pause()
        {
            try
            {
                StopRecognize();
                Status = JobStatus.Paused;
            }
            catch
            {
                // TDOO: notify user
            }
        }

        public override void Stop()
        {
            try
            {
                StopRecognize();
                Status = JobStatus.Stopped;
            }
            catch
            {
                // TODO: notify user
            }
        }
    }
}