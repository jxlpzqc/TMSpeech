using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMSpeech.Core.Plugins
{
    public interface IPlugin
    {
        string GUID { get; }
        string Name { get; }
        string Description { get; }
        string Version { get; }
        string SupportVersion { get; }
        string Author { get; }
        string Url { get; }
        string License { get; }
        string Note { get; }
        IPluginConfigEditor CreateConfigEditor();
        void LoadConfig(string config);
        bool Available { get; }
        void Init();
        void Destroy();
    }
}