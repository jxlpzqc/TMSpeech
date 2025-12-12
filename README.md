# TMSpeech

(旧版)视频演示：https://www.bilibili.com/video/BV1rX4y1p7Nx/

关键词：语音转文字，实时字幕，会议语音识别，歌词字幕展示，识别历史记录查看

`TMSpeech` 是一个Windows下的中文实时语音字幕，通过WASAPI的CaptureLoopback捕获电脑声音（录内音），将语音实时转文字，并以歌词字幕的形式展示。即使完全关闭电脑声音也能使用。

你可以：
- 开会时更放心地走神，突然被喊到的时候不会那么不知所措，只需要看一看识别的历史记录。（本项目的名字来源于此）
- 会议实时转录，自动生成会议纪要，并保存到文件。默认会将识别结果按日期保存到“我的文档”的`TMSpeechLogs`文件夹中

基于[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx/)项目二次开发。实测在我的AMD 5800u的笔记本上CPU占用不到5%。

再次感谢[sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx/)项目的语音识别框架和模型。

## 展示

无边框窗口，可任意拖动和调整大小

![正常识别窗口](imgs/main.png)

历史记录页面（双击可复制）：

![历史记录页面](imgs/history.png)

设置页面：

![设置页面](imgs/settings.png)

## 使用

在[Release](https://github.com/jxlpzqc/TMSpeech/releases)页面中下载最新的release解压，运行`TMSpeech.GUI.exe`即可。在桌面创建快捷方式，使用起来更加方便。

## 基于自定义外部命令的识别

在设置中选用“命令行识别器”。它基于程序和参数，启动子进程，并将标准输出（stdout）作为字幕格式识别，将标准错误输出（stderr）作为日志文件记录（都使用UTF-8编码）。

使用单个换行（'\n'）更新当前句子，使用多个换行（'\n\n'）表示当前行识别结束，样例输出如下：

```
一二
一二三四
一二三四五六七

七六
七六五四
七六五四三二一

```

参考python代码如下：

```diff
+ class MyPrinter:
+     def __init__(self):
+         self.prev_result = ""
+ 
+     def do_print(self, result):
+         if result and self.prev_result != result:
+             self.prev_result = result
+             print(result, end='\n', flush=True)
+ 
+     def on_endpoint(self):
+         print("\n", end="", flush=True)
+ 
+     printer = MyPrinter()
    with sd.InputStream(channels=1, dtype="float32", samplerate=sample_rate, device=device) as s:
        while True:
            samples, _ = s.read(samples_per_read)  # a blocking read
            samples = samples.reshape(-1)
            stream.accept_waveform(sample_rate, samples)
            while recognizer.is_ready(stream):
                recognizer.decode_stream(stream)
            is_endpoint = recognizer.is_endpoint(stream)
            result = recognizer.get_result(stream)

+           printer.do_print(result)
            if is_endpoint:
+               if result:
+                   printer.on_endpoint()
                recognizer.reset(stream)
```

注意事项：

1. 单个换行结尾的行是临时结果，只有多个换行结尾的行才会被存储到历史记录中，这种方式允许模型在后面纠正前面的识别结果。
1. 基于该方式需要子进程独立获取语音源。在设置中切换语音源将不会生效。
1. 程序接受多个参数时，使用空格分割，如果参数本身包含空格，比如带有空格的路径，则可能会出现问题，需要通过双引号转义。详见[这里](https://stackoverflow.com/questions/15061854/how-to-pass-multiple-arguments-in-processstartinfo)和[这里](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.processstartinfo.arguments?view=net-10.0)
1. 程序指定为批处理脚本（'.bat'）时，记得前面加上@隐藏命令显示，同时不要在结尾加入`pause`这种命令（无法检测命令的退出）。

    ```bat
    @python ./speech-recognition-from-microphone-with-endpoint-detection.py
    ```


## 我们需要你的反馈

觉得很有用？但是还有不完美的地方？欢迎点击这里[创建Discussion](https://github.com/jxlpzqc/TMSpeech/discussions/new)、提出反馈！

- 识别准确率不高？
    - 这可能需要更好的模型。当前我们支持sherpa-onnx的流式模型，可以在[这里](https://k2-fsa.github.io/sherpa/onnx/pretrained_models/online-transducer/zipformer-transducer-models.html)下载其他模型，并在设置中修改模型路径。
    - [想要用自己的模型？](https://github.com/jxlpzqc/TMSpeech/issues/6) 如果你发现了效果更好的开源模型，也欢迎推荐给我们！
- 还需要更多功能？
    - 请点击这里[创建issue](https://github.com/jxlpzqc/TMSpeech/issues/new)告诉我们！
    - 如果你懂Windows/C#开发，欢迎提交pull request，开发的过程中遇到任何问题可以创建issue和我们讨论。

## 带模型的Release打包流程

- 在github actions中下载构建好的安装包
- 放入正确的模型文件夹。放入正确的default_config.json，对应上当前的识别器。
- 打包为zip文件，在开发电脑，和另外一台电脑上测试各种功能。
