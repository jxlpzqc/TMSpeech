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
    class SherpaOnnxConfig
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("encoder")]
        public string Encoder { get; set; } = "models\\encoder.onnx";

        [JsonPropertyName("decoder")]
        public string Decoder { get; set; } = "models\\decoder.onnx";

        [JsonPropertyName("joiner")]
        public string Joiner { get; set; } = "models\\joiner.onnx";

        [JsonPropertyName("tokens")]
        public string Tokens { get; set; } = "models\\tokens.txt";
    }

    public class SherpaOnnxConfigEditor : IPluginConfigEditor
    {
        private SherpaOnnxConfig _config = new SherpaOnnxConfig();

        public void SetValue(string key, object value)
        {
            if (key == "model")
            {
                _config.Model = (string)value;
                FormItemsUpdated?.Invoke(this, EventArgs.Empty);
            }

            if (key == "encoder") _config.Encoder = (string)value;
            if (key == "decoder") _config.Decoder = (string)value;
            if (key == "joiner") _config.Joiner = (string)value;
            if (key == "tokens") _config.Tokens = (string)value;
        }

        public object GetValue(string key)
        {
            if (key == "model") return _config.Model;
            if (key == "encoder") return _config.Encoder;
            if (key == "decoder") return _config.Decoder;
            if (key == "joiner") return _config.Joiner;
            if (key == "tokens") return _config.Tokens;
            return "";
        }

        public IReadOnlyList<PluginConfigFormItem> GetFormItems()
        {
            var models = Core.Services.Resource.ResourceManagerFactory.Instance.GetLocalModuleInfos().Result
                .Where(u => u.Type == Core.Services.Resource.ModuleInfoTypeEnums.SherpaOnnxModel);

            var options = new Dictionary<object, string>
            {
                { "", "自定义模型" },
            }.Union(
                models.Select(x => new KeyValuePair<object, string>(x.ID,
                    $"{x.Name} ({x.ID})"))
            ).ToDictionary(x => x.Key, x => x.Value);

            if (!string.IsNullOrEmpty(_config.Model))
            {
                return new PluginConfigFormItem[]
                {
                    new PluginConfigFormItemOption("model", "模型", options)
                };
            }

            return new PluginConfigFormItem[]
            {
                new PluginConfigFormItemOption("model", "模型", options),
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
            return new Dictionary<string, object>()
            {
                { "model", _config.Model },
                { "encoder", _config.Encoder },
                { "decoder", _config.Decoder },
                { "joiner", _config.Joiner },
                { "tokens", _config.Tokens }
            };
        }

        public void LoadConfigString(string data)
        {
            try
            {
                _config = JsonSerializer.Deserialize<SherpaOnnxConfig>(data);
            }
            catch
            {
                _config = new SherpaOnnxConfig();
            }
        }

        public string GenerateConfig()
        {
            return JsonSerializer.Serialize(_config, new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            });
        }
    }
}