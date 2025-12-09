# TMSpeech 架构文档

## 项目概述

TMSpeech 是一个基于 .NET 6 的 Windows 实时语音识别和字幕展示应用，采用插件化架构设计。项目使用 Avalonia UI 框架构建跨平台界面，集成 sherpa-onnx 进行离线语音识别。

## 项目结构

项目采用三层架构设计，分为三个主要子项目：

```
TMSpeech/
├── src/
│   ├── TMSpeech.Core/          # 核心业务逻辑层
│   ├── TMSpeech.GUI/           # 用户界面层
│   ├── TMSpeech/               # 应用程序入口
│   └── Plugins/                # 插件实现
│       ├── TMSpeech.AudioSource.Windows/
│       └── TMSpeech.Recognizer.SherpaOnnx/
```

## 三个子项目的关系

### 1. TMSpeech.Core (核心层)

**职责**：提供核心业务逻辑和基础设施

**主要功能**：
- 插件系统基础架构
- 配置管理
- 任务调度
- 资源管理
- 通知服务

**源码文件清单**：

#### 1.1 插件系统 (Plugins/)

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `Plugins/IPlugin.cs:9-26` | 插件基础接口定义 | GUID, Name, Version, Author, Available, Init(), Destroy(), CreateConfigEditor(), LoadConfig() |
| `Plugins/IRunable.cs:4-8` | 可运行接口，定义生命周期 | Start(), Stop(), ExceptionOccured 事件 |
| `Plugins/IAudioSource.cs:14-19` | 音频源插件接口 | StatusChanged, DataAvailable 事件，继承 IPlugin 和 IRunable |
| `Plugins/IRecognizer.cs:26-36` | 识别器插件接口 | TextChanged, SentenceDone 事件，Feed() 方法接收音频数据 |
| `Plugins/ITranslator.cs` | 翻译器插件接口（未实现） | Translate() 方法 |
| `Plugins/IPluginConfigEditor.cs` | 插件配置编辑器接口 | GetFormItems(), GetAll(), SetValue(), GenerateConfig(), LoadConfigString() |
| `Plugins/PluginConfigFormItem.cs` | 插件配置表单项定义 | PluginConfigFormItemText, PluginConfigFormItemOption, PluginConfigFormItemFile 等多种表单项类型 |
| `Plugins/PluginManager.cs:194-230` | 插件管理器，动态加载和管理插件 | LoadPlugins(), Plugins, AudioSources, Recognizers, Translators 属性，PluginLoadContext 内部类处理程序集加载 |

#### 1.2 配置管理

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `ConfigManager.cs:70-192` | 配置管理基类和本地实现 | Apply(), Get(), BatchApply(), Load(), Save(), ConfigChanged 事件 |
| `ConfigTypes.cs` | 配置键定义和默认值 | GeneralConfigTypes, AppearanceConfigTypes, NotificationConfigTypes, AudioSourceConfigTypes, RecognizerConfigTypes |

#### 1.3 任务管理

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `JobManager.cs:55-277` | 任务管理器，协调音频源和识别器的工作流程 | Start(), Pause(), Stop(), Status 属性，TextChanged, SentenceDone 事件，敏感词检测，日志记录 |

#### 1.4 资源管理 (Services/Resource/)

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `Services/Resource/ResourceManager.cs:50-178` | 资源管理器，管理插件和模型资源 | GetLocalResources(), GetAllResources(), RemoveResource(), 扫描本地和远程资源 |
| `Services/Resource/ModuleInfo.cs:11-109` | 模块元数据定义 | ID, Version, Name, Type, Assemblies, InstallSteps, SherpaOnnxModelPath |
| `Services/Resource/DownloadManager.cs` | 下载和安装管理器 | StartJob(), PauseJob(), DoDownload(), DoExtract(), DoWriteFile()，支持多任务队列 |

#### 1.5 通知服务 (Services/Notification/)

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `Services/Notification/INotificationService.cs` | 通知服务接口 | Notify() 方法，NotificationType 枚举 |
| `Services/Notification/NotificationManager.cs` | 通知管理器（单例） | RegistService(), Notify(), SetNotifyLevel() |

#### 1.6 自动更新 (Services/AutoUpdate/)

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `Services/AutoUpdate/AutoUpdateManager.cs` | 自动更新管理（TODO 未实现） | CheckUpdate() |

#### 1.7 工具类 (Utils/)

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `Utils/SystemObjectNewtonsoftCompatibleConverter.cs` | JSON 转换器，兼容 Newtonsoft.Json | JsonConverter<object> 实现，处理基本类型和 JsonElement |

**依赖关系**：
- 无依赖其他子项目
- 依赖外部包：Downloader、SharpCompress

### 2. TMSpeech.GUI (界面层)

**职责**：提供用户界面和交互体验

**主要功能**：
- 主窗口（实时字幕显示）
- 配置窗口
- 历史记录窗口
- 托盘菜单
- 插件配置界面动态生成

**源码文件清单**：

#### 2.1 应用程序入口

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `App.axaml.cs:22-82` | Avalonia 应用程序入口 | Initialize() 初始化配置，OnFrameworkInitializationCompleted() 加载插件和启动识别，UpdateTrayMenu() |
| `App.axaml` | 应用程序资源定义 | 样式、主题、全局资源 |
| `DefaultConfig.cs` | 生成默认配置 | GenerateConfig() 合并各配置类型，设置默认音频源和字体 |

#### 2.2 视图模型 (ViewModels/)

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `ViewModels/ViewModelBase.cs` | ReactiveUI 视图模型基类 | ViewModelActivator |
| `ViewModels/MainViewModel.cs:99-211` | 主窗口视图模型 | Status, Text, IsLocked, HistoryTexts, PlayCommand, PauseCommand, StopCommand, LockCommand, CaptionStyleViewModel |
| `ViewModels/ConfigViewModel.cs` | 配置窗口视图模型 | GeneralSectionConfig, AppearanceSectionConfig, AudioSectionConfig, RecognizeSectionConfig, NotificationConfig |
| `ViewModels/ResourceManagerViewModel.cs` | 资源管理器视图模型 | Items (ResourceItemViewModel 列表), Loading, LoadCommand |

**ConfigViewModel 包含的配置节视图模型**：
- `SectionConfigViewModelBase`: 配置节基类，实现序列化/反序列化
- `GeneralSectionConfigViewModel`: 通用设置（语言、启动选项、日志路径）
- `AppearanceSectionConfigViewModel`: 外观设置（字体、颜色、阴影、对齐）
- `AudioSectionConfigViewModel`: 音频源配置（动态加载插件）
- `RecognizeSectionConfigViewModel`: 识别器配置（动态加载插件）
- `NotificationConfigViewModel`: 通知设置（通知类型、敏感词）

#### 2.3 视图 (Views/)

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `Views/MainWindow.axaml.cs:12-139` | 主窗口代码后台 | SetCaptionLock() Win32 API 实现窗口穿透，无边框窗口拖拽和调整大小（BeginResizeDrag） |
| `Views/MainWindow.axaml` | 主窗口布局 | 字幕显示、控制按钮、历史记录 |
| `Views/ConfigWindow.axaml.cs` | 配置窗口代码后台 | 显示版本信息（GitVersionInformation） |
| `Views/ConfigWindow.axaml` | 配置窗口布局 | 多标签页配置界面 |
| `Views/HistoryWindow.axaml.cs` | 历史记录窗口代码后台 | 构造函数接收 MainViewModel |
| `Views/HistoryWindow.axaml` | 历史记录窗口布局 | 列表显示历史识别文本 |
| `Views/ResourceManagerView.axaml.cs` | 资源管理视图代码后台 | 用户控件 |
| `Views/ResourceManagerView.axaml` | 资源管理视图布局 | 资源列表、安装/卸载按钮、进度显示 |

#### 2.4 控件 (Controls/)

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `Controls/CaptionView.axaml.cs` | 字幕显示控件 | ShadowColor, ShadowSize, FontColor, TextAlign, Text 属性 |
| `Controls/CaptionView.axaml` | 字幕控件布局 | 文本块样式、阴影效果 |
| `Controls/HistoryView.axaml.cs` | 历史记录视图控件 | 文本选择逻辑（鼠标拖拽选择），Copy(), SelectAll() 方法，RelayCommand 内部类 |
| `Controls/HistoryView.axaml` | 历史记录控件布局 | ListBox 显示历史文本 |
| `Controls/PluginConfigView.cs:15-219` | 插件配置视图（动态生成） | 根据 IPluginConfigEditor.GetFormItems() 动态创建表单控件，支持 TextBox, FilePicker, ComboBox |
| `Controls/FilePicker.axaml.cs` | 文件/文件夹选择器控件 | Text 属性，ExtendedOptions 支持特殊路径（?appdata, ?program 等），FileChanged 事件 |
| `Controls/FilePicker.axaml` | 文件选择器布局 | 文本框 + 浏览按钮 |
| `Controls/AutoGrid.cs` | 自动网格布局控件 | 自动生成行列，支持 Orientation, ChildMargin, RowCount, ColumnCount |
| `Controls/TrayMenu.cs` | 系统托盘菜单 | UpdateItems() 动态更新菜单项，Exit(), UnlockCaption(), ResetWindowLocation() |

#### 2.5 转换器 (Converters/)

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `Converters/ColorToIntConverter.cs` | 颜色与整数互转 | IValueConverter 实现，Convert(), ConvertBack() |

#### 2.6 资源文件

| 文件路径 | 作用 |
|---------|------|
| `IconResources.axaml` | 图标资源定义 |
| `ImageResources.axaml` | 图像资源定义 |

**依赖关系**：
- 依赖 TMSpeech.Core 项目
- 依赖外部包：Avalonia、ReactiveUI、MessageBox.Avalonia

### 3. TMSpeech (应用程序入口)

**职责**：组装应用程序，作为可执行入口

**主要功能**：
- 程序启动入口
- 服务初始化
- 插件构建和部署
- 应用程序打包配置

**源码文件清单**：

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `Program.cs:9-31` | 主程序入口（Main 方法） | 初始化日志追踪，调用 Initializer.InitialzeServices()，配置 Avalonia，异常处理 |
| `Services/Initializer.cs:5-11` | 服务初始化器 | InitialzeServices() 注册通知服务 |
| `Services/NotificationService.cs` | 桌面通知服务实现 | INotificationService 实现，使用 DesktopNotifications 库 |
| `TMSpeech.csproj:83-94` | MSBuild 项目文件 | 构建后自动编译所有插件项目到 plugins 目录，删除重复的 TMSpeech.Core 依赖 |
| `TMSpeech.csproj:96-105` | MSBuild 发布配置 | 发布时将插件复制到发布目录 |

**依赖关系**：
- 依赖 TMSpeech.GUI 项目（通过项目引用）
- TMSpeech.GUI 依赖 TMSpeech.Core（传递依赖）
- 依赖外部包：DesktopNotifications.Avalonia、NetBeauty（用于优化发布包结构）

**项目关系图**：

```
TMSpeech (EXE)
    └─→ TMSpeech.GUI
            └─→ TMSpeech.Core
                    ↑
                    │ (plugin interfaces)
                    │
                Plugins/
                ├─→ TMSpeech.AudioSource.Windows
                ├─→ TMSpeech.Recognizer.SherpaOnnx
                └─→ TMSpeech.Recognizer.Command
```

## 插件实现详解

### 4.1 TMSpeech.AudioSource.Windows (Windows 音频源)

**职责**：提供 Windows 平台的音频采集功能

**插件信息**：
- ID: `TMSpeech.AudioSource.Windows`
- 版本: `1.0.0`
- 类型: 音频源插件 (IAudioSource)
- 作者: Built-in
- 许可: MIT License

**工作原理**：
- 使用 NAudio 库采集音频
- 支持两种音频源：麦克风输入和系统内录（Loopback）
- 输出格式：16kHz 单声道 IEEE Float

**源码文件**：

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `MicrophoneAudioSource.cs` | 麦克风音频输入 | 使用 NAudio WasapiCapture，16kHz 单声道 IEEE Float 格式，Start(), Stop() |
| `LoopbackAudioSource.cs` | 系统内录音频输入 | 使用 NAudio WasapiLoopbackCapture，捕获系统播放声音 |
| `MicrophoneConfigEditor.cs` | 麦克风配置编辑器 | 设备选择器（枚举系统音频设备） |
| `tmmodule.json` | 模块元数据 | 插件 ID, 版本, 名称, 程序集列表 |

---

### 4.2 TMSpeech.Recognizer.SherpaOnnx (Sherpa-Onnx 识别器)

**职责**：使用 Sherpa-Onnx 进行离线语音识别（CPU）

**插件信息**：
- ID: `3002EE6C-9770-419F-A745-E3148747AF4C`
- 版本: `1.0.0`
- 类型: 识别器插件 (IRecognizer)
- 作者: Built-in
- 许可: MIT License

**工作原理**：
- 使用 sherpa-onnx 库进行实时语音识别
- 支持流式识别，通过 Feed() 方法接收音频数据
- 使用 endpoint 检测判断句子结束
- CPU 计算，支持多种 ONNX 模型

**源码文件**：

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `SherpaOnnxRecognizer.cs` | Sherpa-Onnx 离线识别器（CPU） | OnlineRecognizer, OnlineStream, Feed() 接收音频，Run() 识别循环，endpoint 检测 |
| `SherpaOnnxConfigEditor.cs` | 配置编辑器 | 模型选择（从 ResourceManager 获取本地模型）或自定义模型路径 |
| `tmmodule.json` | 模块元数据 | 插件 ID, 版本, 名称 |

---

### 4.3 TMSpeech.Recognizer.SherpaNcnn (Sherpa-Ncnn 识别器)

**职责**：使用 Sherpa-Ncnn 进行离线语音识别（可用 GPU）

**插件信息**：
- ID: `94C23641-CBE0-42B6-9654-82DA42D519F3`
- 版本: `1.0.0`
- 类型: 识别器插件 (IRecognizer)
- 作者: Built-in
- 许可: MIT License

**工作原理**：
- 使用 sherpa-ncnn 库进行实时语音识别
- 支持 Vulkan GPU 加速
- 使用 encoder/decoder/joiner 三模型架构
- 需要 tokens 文件定义词表

**源码文件**：

| 文件路径 | 作用 | 关键成员 |
|---------|------|---------|
| `SherpaNcnnRecognizer.cs` | Sherpa-Ncnn 离线识别器（可用 GPU） | OnlineRecognizer, OnlineStream, UseVulkanCompute 支持 GPU 加速 |
| `SherpaNcnnConfigEditor.cs` | 配置编辑器 | encoder/decoder/joiner 三个模型文件 + tokens 文件配置 |
| `tmmodule.json` | 模块元数据 | 插件 ID, 版本, 名称 |

---

### 4.4 TMSpeech.Recognizer.Command (命令行识别器)

**职责**：通过自定义命令行程序获取识别结果

**插件信息**：
- ID: `TMSpeech.Recognizer.Command`
- 版本: `1.0.0`
- 类型: 识别器插件 (IRecognizer)
- 作者: Built-in
- 许可: MIT License

**工作原理**：
- 不使用 Feed 方法接收音频数据，由外部命令自己负责音频采集
- 通过标准输出 (stdout) 读取识别结果：
  - `\r` (回车): 表示临时结果更新，触发 `TextChanged` 事件
  - `\n` (换行): 表示句子完成，触发 `SentenceDone` 事件
- 支持将标准错误 (stderr) 输出写入日志文件

**源码文件**：

| 文件路径 | 作用 |
|---------|------|
| `tmmodule.json` | 模块元数据 | 插件 ID, 版本, 名称, 描述 |
| `TMSpeech.Recognizer.Command.csproj` | .NET 6.0 项目文件 | 引用 TMSpeech.Core 核心库 |
| `CommandRecognizer.cs` | 识别器主实现 | 启动外部进程，读取 stdout（\r = 临时结果，\n = 句子完成），stderr 写入日志 |
| `CommandRecognizerConfigEditor.cs` | 配置编辑器实现 | Command, Arguments, WorkingDirectory, LogFile 配置项 |

---

## 开发指南

### 添加新插件

1. 创建新的 Class Library 项目
2. 引用 `TMSpeech.Core` 项目
3. 实现对应接口 (IAudioSource, IRecognizer, ITranslator)
4. 创建 `tmmodule.json` 文件定义元数据
5. 在 `TMSpeech.csproj` 中添加插件编译任务

### 添加新配置项

1. 在 `ConfigTypes.cs` 中定义配置键和默认值
2. 在 `DefaultConfig.cs` 中添加到默认配置
3. 在对应的 `SectionConfigViewModel` 中添加属性
4. 在对应的 AXAML 视图中添加 UI 控件

### 修改插件配置界面

1. 在插件的 `ConfigEditor` 中修改 `GetFormItems()` 返回的表单项
2. `PluginConfigView.cs` 会自动根据表单项生成界面
3. 支持的表单项类型：Text, File, Option, Checkbox 等
