using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TMSpeech.Core.Plugins;

namespace TMSpeech.Recognizer.Command;

public class CommandRecognizerConfig
{
    public string Command { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string LogFile { get; set; } = "";
}

public class CommandRecognizer : IRecognizer
{
    public string GUID => "A1B2C3D4-5E6F-7890-ABCD-EF1234567890";
    public string Name => "命令行识别器";
    public string Description => "通过自定义命令行程序获取识别结果，支持 \\r 更新临时结果，\\n 表示句子完成";
    public string Version => "1.0.0";
    public string SupportVersion => "any";
    public string Author => "Built-in";
    public string Url => "";
    public string License => "MIT License";
    public string Note => "使用外部命令进行语音识别";

    public bool Available => true;

    public event EventHandler<SpeechEventArgs>? TextChanged;
    public event EventHandler<SpeechEventArgs>? SentenceDone;
    public event EventHandler<Exception>? ExceptionOccured;

    private CommandRecognizerConfig _config = new();
    private Process? _process;
    private Thread? _readThread;
    private Thread? _errorReadThread;
    private bool _isRunning;
    private readonly StringBuilder _currentLine = new();
    private readonly object _lockObject = new();
    private StreamWriter? _logWriter;

    public IPluginConfigEditor CreateConfigEditor() => new CommandRecognizerConfigEditor();

    public void LoadConfig(string config)
    {
        if (!string.IsNullOrEmpty(config))
        {
            try
            {
                _config = JsonSerializer.Deserialize<CommandRecognizerConfig>(config) ?? new();
            }
            catch
            {
                _config = new CommandRecognizerConfig();
            }
        }
    }

    public void Init()
    {
        // 初始化
    }

    public void Destroy()
    {
        Stop();
    }

    public void Feed(byte[] data)
    {
        // 命令行识别器不使用 Feed 方法
        // 外部命令自己负责音频采集
    }

    public void Start()
    {
        if (_isRunning)
        {
            throw new InvalidOperationException("识别器已在运行中");
        }

        if (string.IsNullOrWhiteSpace(_config.Command))
        {
            throw new InvalidOperationException("未配置命令");
        }

        try
        {
            _isRunning = true;

            // 打开日志文件（如果配置了）
            if (!string.IsNullOrWhiteSpace(_config.LogFile))
            {
                try
                {
                    var logDir = Path.GetDirectoryName(_config.LogFile);
                    if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                    }
                    _logWriter = new StreamWriter(_config.LogFile, append: true, Encoding.UTF8)
                    {
                        AutoFlush = true
                    };
                }
                catch (Exception ex)
                {
                    // 日志文件打开失败不影响主功能
                    System.Diagnostics.Debug.WriteLine($"无法打开日志文件: {ex.Message}");
                }
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _config.Command,
                Arguments = _config.Arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (!string.IsNullOrWhiteSpace(_config.WorkingDirectory))
            {
                startInfo.WorkingDirectory = _config.WorkingDirectory;
            }

            _process = new Process { StartInfo = startInfo };
            _process.Start();

            // 启动标准输出读取线程
            _readThread = new Thread(ReadOutputLoop)
            {
                IsBackground = true
            };
            _readThread.Start();

            // 启动标准错误读取线程（如果配置了日志文件）
            if (_logWriter != null)
            {
                _errorReadThread = new Thread(ReadErrorLoop)
                {
                    IsBackground = true
                };
                _errorReadThread.Start();
            }
        }
        catch (Exception ex)
        {
            _isRunning = false;
            throw new InvalidOperationException($"启动命令失败: {ex.Message}", ex);
        }
    }

    public void Stop()
    {
        _isRunning = false;

        try
        {
            _process?.Kill(true);
        }
        catch
        {
            // 忽略终止进程时的错误
        }

        _process?.Dispose();
        _process = null;

        _readThread?.Join(1000);
        _readThread = null;

        _errorReadThread?.Join(1000);
        _errorReadThread = null;

        _logWriter?.Dispose();
        _logWriter = null;

        lock (_lockObject)
        {
            _currentLine.Clear();
        }
    }

    private void ReadOutputLoop()
    {
        try
        {
            if (_process?.StandardOutput == null) return;

            var buffer = new char[1];
            while (_isRunning && !_process.HasExited)
            {
                var readCount = _process.StandardOutput.Read(buffer, 0, 1);
                if (readCount <= 0) continue;

                var ch = buffer[0];

                lock (_lockObject)
                {
                    if (ch == '\r')
                    {
                        // \r 表示用当前行替换之前的临时结果
                        var text = _currentLine.ToString();
                        _currentLine.Clear();

                        if (!string.IsNullOrEmpty(text))
                        {
                            var textInfo = new TextInfo(text);
                            TextChanged?.Invoke(this, new SpeechEventArgs { Text = textInfo });
                        }
                    }
                    else if (ch == '\n')
                    {
                        // \n 表示一句话完成
                        var text = _currentLine.ToString();
                        _currentLine.Clear();

                        if (!string.IsNullOrEmpty(text))
                        {
                            var textInfo = new TextInfo(text);
                            SentenceDone?.Invoke(this, new SpeechEventArgs { Text = textInfo });
                        }
                    }
                    else
                    {
                        // 普通字符，追加到当前行
                        _currentLine.Append(ch);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (_isRunning)
            {
                ExceptionOccured?.Invoke(this, ex);
            }
        }
    }

    private void ReadErrorLoop()
    {
        try
        {
            if (_process?.StandardError == null || _logWriter == null) return;

            var buffer = new char[256];
            while (_isRunning && !_process.HasExited)
            {
                var readCount = _process.StandardError.Read(buffer, 0, buffer.Length);
                if (readCount <= 0) continue;

                var text = new string(buffer, 0, readCount);
                _logWriter.Write(text);
            }
        }
        catch (Exception ex)
        {
            // stderr 读取失败不影响主功能
            System.Diagnostics.Debug.WriteLine($"stderr 读取失败: {ex.Message}");
        }
    }
}
