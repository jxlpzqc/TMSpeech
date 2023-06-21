using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        private SpeechCore _core;
        private Thread _thread;


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _core = new SpeechCore();
            _core.TextChanged += _core_TextChanged;
            _core.UpdateList += _core_UpdateList;

            _thread = new Thread(() =>
            {
                _core.Init();
            });
            _thread.Start();
        }

        private void _core_UpdateList(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                listHistory.ItemsSource = _core.AllText;
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
            if(_historyShown)
            {
                listHistory.Visibility = Visibility.Collapsed;
                this.Height -= 100;
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
