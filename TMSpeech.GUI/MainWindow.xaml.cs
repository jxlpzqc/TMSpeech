using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            if (e.LeftButton== MouseButtonState.Pressed && _mouseLocation != e.GetPosition(this))
            {
                _isDrag = true;
                DragMove();
            }

        }

        private void gridContainer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if(_isDrag)
            {
                _isDrag = false;
                e.Handled = true;
            }

        }
    }
}
