using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TMSpeech.Core.Plugins;

namespace TMSpeech.Recognizer.SherpaOnnx
{
    public class SherpaOnnxConfigEditor : IPluginConfigEditor
    {
        private Dictionary<string, object> _config = new Dictionary<string, object>();

        public void SetValue(string key, object value)
        {
            _config[key] = value.ToString();
        }

        public object GetValue(string key)
        {
            return _config[key];
        }

        public IReadOnlyList<PluginConfigFormItem> GetFormItems()
        {
            return new PluginConfigFormItem[]
            {
                new PluginConfigFormItemText("model", "模型"),
                new PluginConfigFormItemFile("encoder", "编码器"),
                new PluginConfigFormItemFile("decoder", "解码器"),
                new PluginConfigFormItemFile("joiner", "连接器"),
                new PluginConfigFormItemFile("tokens", "词表")
            };
        }

        public event EventHandler<EventArgs>? FormItemsUpdated;
        
        public event EventHandler<EventArgs>? ValueUpdated;

        IReadOnlyDictionary<string, object> IPluginConfigEditor.GetAll()
        {
            return _config;
        }

        public void LoadConfigString(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                _config = new Dictionary<string, object>()
                {
                    { "model", "" },
                    { "encoder", "<path>" },
                    { "decoder", "<path>" },
                    { "joiner", "<path>" },
                    { "tokens", "<path>" }
                };
                return;
            }

            _config = JsonSerializer.Deserialize<Dictionary<string, object>>(data);
        }


        public string GenerateConfig()
        {
            return JsonSerializer.Serialize(_config);
        }

        public void Set(string key, string value)
        {
            if (key == "models")
            {
                _config["models"] = value;
                //TODO: read other config

                ValueUpdated?.Invoke(this, null);
            }
            else
            {
                _config[key] = value;
                _config["models"] = "";

                ValueUpdated?.Invoke(this, null);
            }
        }
    }
}