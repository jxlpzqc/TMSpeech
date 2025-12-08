using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TMSpeech.Core.Plugins;

namespace TMSpeech.Recognizer.SherpaNcnn
{


    /// <summary>
    /// Sherpa-NCNN 配置数据类
    /// </summary>
    class SherpaNcnnConfig
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("encoder_param")]
        public string EncoderParam { get; set; } = "models\\encoder.param";

        [JsonPropertyName("encoder_bin")]
        public string EncoderBin { get; set; } = "models\\encoder.bin";

        [JsonPropertyName("decoder_param")]
        public string DecoderParam { get; set; } = "models\\decoder.param";

        [JsonPropertyName("decoder_bin")]
        public string DecoderBin { get; set; } = "models\\decoder.bin";

        [JsonPropertyName("joiner_param")]
        public string JoinerParam { get; set; } = "models\\joiner.param";

        [JsonPropertyName("joiner_bin")]
        public string JoinerBin { get; set; } = "models\\joiner.bin";

        [JsonPropertyName("tokens")]
        public string Tokens { get; set; } = "models\\tokens.txt";
    }

    /// <summary>
    /// Sherpa-NCNN 配置编辑器
    /// </summary>
    public class SherpaNcnnConfigEditor : IPluginConfigEditor
    {
        private SherpaNcnnConfig _config = new SherpaNcnnConfig();

        public void SetValue(string key, object value)
        {
            switch (key)
            {
                case "model":
                    _config.Model = (string)value;
                    FormItemsUpdated?.Invoke(this, EventArgs.Empty);
                    break;
                case "encoder_param":
                    _config.EncoderParam = (string)value;
                    break;
                case "encoder_bin":
                    _config.EncoderBin = (string)value;
                    break;
                case "decoder_param":
                    _config.DecoderParam = (string)value;
                    break;
                case "decoder_bin":
                    _config.DecoderBin = (string)value;
                    break;
                case "joiner_param":
                    _config.JoinerParam = (string)value;
                    break;
                case "joiner_bin":
                    _config.JoinerBin = (string)value;
                    break;
                case "tokens":
                    _config.Tokens = (string)value;
                    break;
            }
            ValueUpdated?.Invoke(this, EventArgs.Empty);
        }

        public object GetValue(string key)
        {
            return key switch
            {
                "model" => _config.Model,
                "encoder_param" => _config.EncoderParam,
                "encoder_bin" => _config.EncoderBin,
                "decoder_param" => _config.DecoderParam,
                "decoder_bin" => _config.DecoderBin,
                "joiner_param" => _config.JoinerParam,
                "joiner_bin" => _config.JoinerBin,
                "tokens" => _config.Tokens,
                _ => ""
            };
        }

        public IReadOnlyList<PluginConfigFormItem> GetFormItems()
        {
            var task = Core.Services.Resource.ResourceManagerFactory.Instance.GetLocalModuleInfos();
            task.Wait();
            var models = task.Result
                .Where(u => u.Type == Core.Services.Resource.ModuleInfoTypeEnums.SherpaNcnnModel);

            var options = new Dictionary<object, string>
            {
                { "", "自定义模型" }
            }.Union(
                models.Select(x => new KeyValuePair<object, string>(x.ID,
                    $"{x.Name} ({x.ID})"))
            ).ToDictionary(x => x.Key, x => x.Value);

            // 如果选择了预定义模型，只显示模型选择项
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
                new PluginConfigFormItemFile("encoder_param", "编码器参数文件 (.param)"),
                new PluginConfigFormItemFile("encoder_bin", "编码器权重文件 (.bin)"),
                new PluginConfigFormItemFile("decoder_param", "解码器参数文件 (.param)"),
                new PluginConfigFormItemFile("decoder_bin", "解码器权重文件 (.bin)"),
                new PluginConfigFormItemFile("joiner_param", "连接器参数文件 (.param)"),
                new PluginConfigFormItemFile("joiner_bin", "连接器权重文件 (.bin)"),
                new PluginConfigFormItemFile("tokens", "词表文件 (tokens.txt)")
             };
        }

        public event EventHandler<EventArgs>? FormItemsUpdated;
        public event EventHandler<EventArgs>? ValueUpdated;

        IReadOnlyDictionary<string, object> IPluginConfigEditor.GetAll()
        {
            return new Dictionary<string, object>()
            {
                { "model", _config.Model },
                { "encoder_param", _config.EncoderParam },
                { "encoder_bin", _config.EncoderBin },
                { "decoder_param", _config.DecoderParam },
                { "decoder_bin", _config.DecoderBin },
                { "joiner_param", _config.JoinerParam },
                { "joiner_bin", _config.JoinerBin },
                { "tokens", _config.Tokens }
            };
        }

        public void LoadConfigString(string data)
        {
            try
            {
                _config = JsonSerializer.Deserialize<SherpaNcnnConfig>(data) ?? new SherpaNcnnConfig();
            }
            catch
            {
                _config = new SherpaNcnnConfig();
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
