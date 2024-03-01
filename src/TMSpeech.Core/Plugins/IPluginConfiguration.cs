using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMSpeech.Core.Plugins
{
    public class PluginConfigurationMeta
    {
        public enum MetaType
        {
            Text,
            Option,
            File,
            Folder
        }

        public string Key { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public MetaType Type { get; set; }

        /// <summary>
        /// Options for Option type, split by line
        /// Extensions for File type
        /// </summary>
        public string Filter { get; set; }

        public string DefaultValue { get; set; }
    }

    public interface IPluginConfiguration
    {
        IReadOnlyList<PluginConfigurationMeta> ListMeta();
        IReadOnlyDictionary<string, string> GetAll();
        void Set(string key, string value);
        string Get(string key);
        void ResetToDefault();

        event EventHandler<EventArgs> ValueUpdated;
        /// <summary>
        /// Save to string
        /// </summary>
        /// <returns>a string represent configuration</returns>
        string Save();

        /// <summary>
        /// Load from string
        /// </summary>
        /// <param name="data"></param>
        void Load(string data);
    }
}