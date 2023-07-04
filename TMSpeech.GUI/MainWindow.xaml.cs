using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TMSpeech.GUI
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

    interface ISpecchRecognition: IDisposable
    {
        IList<TextInfo> GetAllTexts();
        void SetTextChangedHandler(EventHandler<SpeechEventArgs> handler);
        void SetUpdateListHandler(EventHandler<EventArgs> handler);
        void Run();
        void Clear();
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private Point _mouseLocation;
        private bool _isDrag = false;

        private void gridContainer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _mouseLocation = e.GetPosition(gridContainer);

        }

        private void gridContainer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _mouseLocation != e.GetPosition(this))
            {
                _isDrag = true;
                DragMove();
            }

        }

        private void gridContainer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDrag)
            {
                _isDrag = false;
                e.Handled = true;
            }

        }

        private ISpecchRecognition _core;
        private Thread _thread;

        private string GetRealPath(string path)
        {
            if (System.IO.Path.IsPathRooted(path))
            {
                return path;
            }
            else
            {
                return System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
                    path);
            }

        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = SettingsManager.Read();

            var logpath = GetRealPath(settings.LogSave);

            Directory.CreateDirectory(logpath);

            var logfile = System.IO.Path.Combine(GetRealPath(settings.LogSave),
                string.Format("{0:MM-dd-yy-HH-mm-ss}.txt", DateTime.Now));

            _core = new SpeechCore(encoder: GetRealPath(settings.ModelEncoder),
                       decoder: GetRealPath(settings.ModelDecoder),
                       joiner: GetRealPath(settings.ModelJoiner),
                       tokens: GetRealPath(settings.ModelTokens),
                       savefile: GetRealPath(logfile));
            _core.SetTextChangedHandler(_core_TextChanged);
            _core.SetUpdateListHandler(_core_UpdateList);

            _thread = new Thread(() =>
            {
                try
                {
                    _core.Run();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Recognizer launch error: \n{ex}");
                }
            });
            _thread.Start();
        }

        private void _core_UpdateList(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                listHistory.ItemsSource = _core.GetAllTexts();
                listHistory.Items.Refresh();
                listHistory.SelectedIndex = listHistory.Items.Count - 1;
                listHistory.ScrollIntoView(listHistory.SelectedItem);

            });
        }

        private void _core_TextChanged(object sender, SpeechEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                txtMain.Text = e.Text.Text;
            });

        }

        private bool _historyShown = false;

        private void btnShowHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_historyShown)
            {
                listHistory.Visibility = Visibility.Collapsed;
                if (this.Height - 100 > 30)
                {
                    this.Height -= 100;
                }
            }
            else
            {
                listHistory.Visibility = Visibility.Visible;
                this.Height += 100;
            }

            _historyShown = !_historyShown;
        }

        private void btnPreference_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ConfigWindow();
            dialog.Show();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _core.Dispose();
            Process.GetCurrentProcess().Kill();
        }
    }
}
