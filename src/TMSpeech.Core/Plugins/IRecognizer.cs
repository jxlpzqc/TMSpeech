using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMSpeech.Core.Plugins
{
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

    public class SpeechEventArgs
    {
        public TextInfo Text { get; set; }
    }

    public interface IRecognizer : IPlugin
    {
        event EventHandler<SpeechEventArgs> TextChanged;
        event EventHandler<SpeechEventArgs> SentenceDone;

        /// <summary>
        /// Feed audio data to the recognizer (e.g. from a microphone or a file
        /// </summary>
        /// <param name="data"></param>
        void Feed(byte[] data);
        void Start();

        /// <summary>
        /// Stop and free unmanaged resources
        /// </summary>
        void Stop();
    }
}
