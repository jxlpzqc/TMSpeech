using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace TMSpeech.GUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public void SetResource(string key, object value)
        {
            Resources[key] = value;
        }

        public App()
        {
            InitializeComponent();
            SettingsManager.Apply(SettingsManager.Read());
        }
    }
}
