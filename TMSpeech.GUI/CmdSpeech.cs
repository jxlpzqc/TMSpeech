using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace TMSpeech.GUI
{
    class CmdSpeech : ISpecchRecognition
    {
        public List<TextInfo> AllTexts { get; set; } = new List<TextInfo>();
        public string CurrentText { get; set; }
        public event EventHandler<SpeechEventArgs> TextChanged;
        public event EventHandler<SpeechEventArgs> UpdateList;

        string ExecutablePath;
        string Arguments;
        string WorkingDirectory;
        string SaveFile;

        public CmdSpeech(string Cmdline, string WorkingDirectory, string savefile)
        {
            this.ExecutablePath = Cmdline;
            this.WorkingDirectory = WorkingDirectory;
            this.SaveFile = savefile;
        }

        private bool disposed = false;
        public void Dispose()
        {
            disposed = true;
        }

        public IList<TextInfo> GetAllTexts()
        {
            return new List<TextInfo>(AllTexts);
        }

        public void SetTextChangedHandler(EventHandler<SpeechEventArgs> handler)
        {
            this.TextChanged += handler;
        }

        public void SetUpdateListHandler(EventHandler<SpeechEventArgs> handler)
        {
            this.UpdateList += handler;
        }

        public void Run()
        {
            disposed = false;
            ProcessStartInfo startInfo = new ProcessStartInfo(ExecutablePath, Arguments);
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;
            startInfo.ErrorDialog = true;
            // startInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
            if (!string.IsNullOrEmpty(WorkingDirectory))
            {
                startInfo.WorkingDirectory = WorkingDirectory;
            }
            
            Process p = Process.Start(startInfo);

            // https://stackoverflow.com/questions/4143281/capturing-binary-output-from-process-standardoutput
            byte c = 0;
            StringBuilder sb = new StringBuilder();
            BinaryReader br = new BinaryReader(p.StandardOutput.BaseStream);
            List<byte> buffer = new List<byte>();
            // https://stackoverflow.com/questions/7119561/help-me-throwing-exception-error-in-decoding-code-help-needed
            Encoding encoding = Encoding.GetEncoding(Encoding.UTF8.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            try
            {
                while (!disposed)
                {
                    c = br.ReadByte();
                    buffer.Add(c);
                    string result;
                    try
                    {
                        result = encoding.GetString(buffer.ToArray());
                    } catch (DecoderFallbackException) { continue; }
                    if (result.Last() == '\r')
                    {
                        buffer.Clear();
                    }
                    var item = new TextInfo(result);
                    if (result.Last() == '\n')
                    {
                        AllTexts.Add(item);
                        if (!string.IsNullOrEmpty(SaveFile))
                        {
                            try
                            {
                                File.AppendAllText(SaveFile, string.Format("{0:T}: {1}\n", item.Time, item.Text));
                            }
                            catch { }
                        }
                        UpdateList?.Invoke(this, new SpeechEventArgs()
                        {
                            Text = item,
                        });
                        buffer.Clear();
                    }
                    TextChanged?.Invoke(this, new SpeechEventArgs()
                    {
                        Text = item,
                    });
                }
            }
            catch (EndOfStreamException) { }
            

        }

        public void Clear()
        {
            AllTexts.Clear();
            CurrentText = "";
        }
    }
}
