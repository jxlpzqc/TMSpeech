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
using System.Windows.Shapes;

namespace TMSpeech.GUI
{
    /// <summary>
    /// ConfigWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ConfigWindow : Window
    {
        public ConfigWindow()
        {
            InitializeComponent();
            this.DataContext = SettingsManager.Read();
        }

        private Settings Settings => (Settings)this.DataContext;

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Write(Settings);
            SettingsManager.Apply(Settings);
            this.Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Apply(SettingsManager.Read());
            this.Close();
        }

        private void btnApply_Click(object sender, RoutedEventArgs e)
        {
            SettingsManager.Apply(Settings);
        }
    }

}
