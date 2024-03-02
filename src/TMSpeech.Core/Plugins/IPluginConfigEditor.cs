using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TMSpeech.Core.Plugins
{
    public interface IPluginConfigEditor
    {
        IReadOnlyList<PluginConfigFormItem> GetFormItems();
        event EventHandler<EventArgs> FormItemsUpdated;

        IReadOnlyDictionary<string, object> GetAll();
        void SetValue(string key, object value);
        object GetValue(string key);

        event EventHandler<EventArgs> ValueUpdated;

        /// <summary>
        /// Save to string
        /// </summary>
        /// <returns>a string represent configuration</returns>
        string GenerateConfig();

        /// <summary>
        /// Load from string
        /// </summary>
        /// <param name="config"></param>
        void LoadConfigString(string config);
    }
}