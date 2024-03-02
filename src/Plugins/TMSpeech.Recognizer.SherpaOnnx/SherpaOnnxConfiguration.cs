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
    public class SherpaOnnxConfiguration : IPluginConfiguration
    {
        internal string Encoder => _config.GetValueOrDefault("encoder",
            "D:\\WorkSpace\\SpeechRecognize\\腾讯会议语音识别\\sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20\\encoder-epoch-99-avg-1.onnx"
        );

        internal string Decoder => _config.GetValueOrDefault("decoder",
            "D:\\WorkSpace\\SpeechRecognize\\腾讯会议语音识别\\sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20\\decoder-epoch-99-avg-1.onnx"
        );

        internal string Joiner => _config.GetValueOrDefault("joiner",
            "D:\\WorkSpace\\SpeechRecognize\\腾讯会议语音识别\\sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20\\joiner-epoch-99-avg-1.onnx"
        );

        internal string Tokens => _config.GetValueOrDefault("tokens",
            "D:\\WorkSpace\\SpeechRecognize\\腾讯会议语音识别\\sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20\\tokens.txt"
        );

        private Dictionary<string, string> _config = new Dictionary<string, string>();

        public string Get(string key)
        {
            return _config[key];
        }

        public IReadOnlyDictionary<string, string> GetAll()
        {
            return _config;
        }

        public IReadOnlyList<PluginConfigurationMeta> ListMeta() => new List<PluginConfigurationMeta>
        {
            new PluginConfigurationMeta
            {
                Key = "model",
                DefaultValue = "",
                Name = "模型",
                Type = PluginConfigurationMeta.MetaType.Option,
                Filter = string.Join("\n", new string[] { "自定义" })
            },
            new PluginConfigurationMeta
            {
                Key = "encoder",
                DefaultValue = "",
                Name = "编码器",
                Type = PluginConfigurationMeta.MetaType.File,
            },
            new PluginConfigurationMeta
            {
                Key = "decoder",
                DefaultValue = "",
                Name = "解码器",
                Type = PluginConfigurationMeta.MetaType.File,
            },
            new PluginConfigurationMeta
            {
                Key = "joiner",
                DefaultValue = "",
                Name = "连接器",
                Type = PluginConfigurationMeta.MetaType.File,
            },
            new PluginConfigurationMeta
            {
                Key = "tokens",
                DefaultValue = "",
                Name = "Tokens",
                Type = PluginConfigurationMeta.MetaType.File,
            },
        };

        public void Load(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                _config = new Dictionary<string, string>()
                {
                    { "model", "" },
                    { "encoder", "<path>" },
                    { "decoder", "<path>" },
                    { "joiner", "<path>" },
                    { "tokens", "<path>" }
                };

                return;
            }

            _config = JsonSerializer.Deserialize<Dictionary<string, string>>(data);
        }

        public void ResetToDefault()
        {
        }

        public event EventHandler<EventArgs>? ValueUpdated;

        public string Save()
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