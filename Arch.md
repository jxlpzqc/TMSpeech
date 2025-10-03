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

**关键源码**：

| 文件路径 | 作用 |
|---------|------|
| `Plugins/PluginManager.cs:194-230` | 插件加载器，扫描 plugins 目录下的 dll 文件，使用 AssemblyLoadContext 动态加载插件程序集 |
| `Plugins/IPlugin.cs:9-26` | 插件接口定义，所有插件必须实现此接口，提供元数据（GUID、名称、版本等）和配置功能 |
| `Plugins/IAudioSource.cs:14-19` | 音频源插件接口，继承 IPlugin 和 IRunable，定义音频数据获取规范 |
| `Plugins/IRecognizer.cs:26-36` | 识别器插件接口，继承 IPlugin 和 IRunable，定义语音识别规范，包含 Feed 方法接收音频数据 |
| `Plugins/IRunable.cs:4-8` | 可运行接口，定义 Start/Stop 生命周期和异常处理 |
| `ConfigManager.cs:70-192` | 本地配置管理器，使用 JSON 存储配置，支持键值对操作和变更通知 |
| `JobManager.cs:55-277` | 任务管理器，协调音频源和识别器插件的工作流程，处理语音识别任务的启动、暂停、停止 |
| `Services/Resource/ResourceManager.cs:50-178` | 资源管理器，管理插件和模型的本地安装、远程下载、版本更新 |
| `Services/Resource/ModuleInfo.cs:11-109` | 模块元数据定义，描述插件/模型的信息、安装步骤、文件路径等 |

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

**关键源码**：

| 文件路径 | 作用 |
|---------|------|
| `App.axaml.cs:22-82` | 应用程序入口，初始化配置管理器（28行），启动时加载所有插件（73行），处理启动时自动运行选项（76-79行） |
| `Views/MainWindow.axaml.cs:12-139` | 主窗口实现，提供无边框窗口拖拽调整大小功能（65-116行），调用 Win32 API 实现窗口锁定穿透效果（32-61行） |
| `ViewModels/MainViewModel.cs:99-211` | 主视图模型，使用 ReactiveUI 进行响应式编程，订阅 JobManager 事件更新界面状态，管理播放/暂停/停止命令 |
| `Controls/PluginConfigView.cs:15-219` | 插件配置视图，根据 IPluginConfigEditor 提供的表单定义动态生成配置界面（88-167行），支持文本框、文件选择器、下拉框等控件 |
| `Controls/CaptionView.axaml.cs` | 字幕显示控件，实时显示识别结果 |
| `Controls/HistoryView.axaml.cs` | 历史记录视图，展示已识别的文本历史 |
| `Controls/TrayMenu.cs` | 系统托盘菜单，提供快捷操作入口 |

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

**关键源码**：

| 文件路径 | 作用 |
|---------|------|
| `Program.cs:9-31` | 应用程序主入口，初始化服务（19行），配置 Avalonia 应用程序构建器，设置桌面通知 |
| `Services/Initializer.cs:5-11` | 服务初始化器，注册通知服务等基础设施服务 |
| `Services/NotificationService.cs` | 桌面通知服务实现 |
| `TMSpeech.csproj:83-94` | MSBuild 构建脚本，在构建后自动编译所有插件项目到 plugins 目录（89行），删除重复的 TMSpeech.Core 依赖（93行） |
| `TMSpeech.csproj:96-105` | MSBuild 发布脚本，在发布时将插件复制到发布目录（104行） |

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
                └─→ TMSpeech.Recognizer.SherpaOnnx
```

## 插件系统交互流程

### 1. 插件加载流程

```
[应用启动]
    ↓
[App.axaml.cs:73] PluginManagerFactory.GetInstance().LoadPlugins()
    ↓
[PluginManager.cs:194] LoadPlugins() 方法扫描 plugins 目录
    ↓
[PluginManager.cs:200-229] 遍历子目录，读取 tmmodule.json
    ↓
[PluginManager.cs:85-117] LoadPlugin() 使用 PluginLoadContext 加载程序集
    ↓
[PluginManager.cs:99-116] 查找实现 IPlugin 接口的类型，创建实例并调用 Init()
    ↓
[PluginManager.cs:115] 将插件实例存储到 _plugins 列表
    ↓
[PluginManager.cs:47-60] 通过属性暴露分类字典（AudioSources、Recognizers、Translators）
```

**关键机制**：
- **隔离加载**：使用 `PluginLoadContext : AssemblyLoadContext` (119-192行) 为每个插件创建独立的程序集加载上下文
- **共享核心**：TMSpeech.Core 在所有插件间共享（151行返回 null 使用宿主的 TMSpeech.Core）
- **本地依赖解析**：使用 `AssemblyDependencyResolver` (121行) 解析插件目录下的依赖
- **原生库支持**：`LoadUnmanagedDll` 方法（161-191行）支持加载 runtimes/[rid]/native 下的原生 DLL

### 2. 插件配置流程

```
[用户打开配置界面]
    ↓
[ConfigWindow] 显示所有可用插件
    ↓
[用户选择音频源/识别器插件]
    ↓
[IPlugin.CreateConfigEditor()] 创建配置编辑器实例
    ↓
[IPluginConfigEditor.GetFormItems()] 返回表单项定义
    ↓
[PluginConfigView.cs:88-167] 根据表单项动态生成 UI 控件
    ├─→ PluginConfigFormItemText → TextBox
    ├─→ PluginConfigFormItemFile → FilePicker
    └─→ PluginConfigFormItemOption → ComboBox
    ↓
[用户修改配置]
    ↓
[PluginConfigView:106-152] 控件事件触发 ConfigEditor.SetValue()
    ↓
[PluginConfigView:59] 调用 ConfigEditor.GenerateConfig() 序列化配置
    ↓
[ConfigManager.Apply()] 保存配置到 config.json
```

**配置存储格式**：
```json
{
  "audio.source": "TMSpeech.AudioSource.Windows!3746756F-07D8-4972-BBF7-C443DF1E7E24",
  "plugin.TMSpeech.AudioSource.Windows!3746756F-07D8-4972-BBF7-C443DF1E7E24.config": "{\"deviceID\":\"...\"}"
}
```

### 3. 语音识别工作流程

```
[用户点击开始按钮]
    ↓
[MainViewModel:176] PlayCommand.Execute()
    ↓
[JobManager:248] Start() 方法
    ↓
[JobManager:141-193] StartRecognize() 初始化音频源和识别器
    ├─→ [JobManager:72-83] InitAudioSource()
    │       ├─→ 从配置读取选定的音频源 ID
    │       ├─→ 从 PluginManager 获取插件实例
    │       └─→ 调用 audioSource.LoadConfig() 加载配置
    │
    └─→ [JobManager:94-112] InitRecognizer()
            ├─→ 从配置读取选定的识别器 ID
            ├─→ 从 PluginManager 获取插件实例
            └─→ 调用 recognizer.LoadConfig() 加载配置
    ↓
[JobManager:157-176] 启动识别器和音频源
    ├─→ recognizer.Start()
    └─→ audioSource.Start()
    ↓
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
音频数据流动：

[MicrophoneAudioSource:44-63] 使用 NAudio 捕获麦克风/系统音频
    ↓
[MicrophoneAudioSource:56] DataAvailable 事件发出音频数据
    ↓
[JobManager:88] OnAudioSourceOnDataAvailable() 接收数据
    ↓
[JobManager:91] 调用 recognizer.Feed(data) 传递给识别器
    ↓
[SherpaOnnxRecognizer:51-55] Feed() 方法将数据送入识别流
    ↓
[SherpaOnnxRecognizer:67-148] Run() 后台线程持续处理
    ├─→ [119-122] 调用 recognizer.Decode() 执行识别
    ├─→ [125] 获取识别结果文本
    ├─→ [131-134] TextChanged 事件发出实时结果
    └─→ [138-142] SentenceDone 事件发出完整句子
    ↓
[JobManager:126-139] OnRecognizerOnTextChanged() 处理实时文本
    ├─→ [130-135] 检测敏感词
    └─→ [138] 触发 JobManager.TextChanged 事件
    ↓
[MainViewModel:195-203] 订阅 TextChanged 事件更新 Text 属性
    ↓
[CaptionView] 绑定到 Text 属性，实时显示识别结果
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
    ↓
[JobManager:114-124] OnRecognizerOnSentenceDone() 处理完整句子
    ├─→ [117-120] 保存到日志文件
    └─→ [123] 触发 JobManager.SentenceDone 事件
    ↓
[MainViewModel:205-209] 订阅 SentenceDone 添加到历史记录
```

### 4. 插件生命周期管理

```
[初始化阶段]
    IPlugin.Init() → 插件初始化资源
    ↓
[配置阶段]
    IPlugin.LoadConfig(config) → 加载用户配置
    ↓
[运行阶段]
    IRunable.Start() → 启动插件功能
    ↓
    [音频源] DataAvailable 事件 → 持续产生数据
    [识别器] Feed(data) → 接收数据
              ↓
              TextChanged 事件 → 实时识别结果
              SentenceDone 事件 → 句子完成
    ↓
[停止阶段]
    IRunable.Stop() → 停止插件功能，释放资源
    ↓
[销毁阶段]
    IPlugin.Destroy() → 清理插件资源
```

### 5. 异常处理机制

```
[插件运行时异常]
    ↓
IRunable.ExceptionOccured 事件触发
    ↓
[JobManager:208-213] OnPluginRunningExceptionOccurs()
    ├─→ 发送桌面通知提示用户
    └─→ 调用 Stop() 停止当前任务
    ↓
[MainViewModel:199-201] 捕获命令异常，在字幕中显示错误信息
```

## 资源管理系统

### 模块（Module）概念

模块是 TMSpeech 的扩展单元，包括两类：
1. **插件模块** (type: "plugin")：实现 IAudioSource、IRecognizer 等接口的功能扩展
2. **模型模块** (type: "sherpaonnx_model")：语音识别模型文件包

每个模块包含 `tmmodule.json` 元数据文件，描述模块信息、安装步骤等。

### 资源存储位置

- **内置资源**：`[应用目录]/plugins/` （CanRemove = false）
- **用户安装资源**：`%AppData%/TMSpeech/plugins/` （CanRemove = true）

### 资源获取流程

```
[SherpaOnnxRecognizer:75-82] 需要加载模型
    ↓
ResourceManagerFactory.Instance.GetLocalResource(modelId)
    ↓
[ResourceManager:85-122] 扫描两个目录，读取 tmmodule.json
    ↓
返回 Resource 对象（包含 LocalDir、ModuleInfo）
    ↓
[SherpaOnnxRecognizer:79-82] 拼接模型文件路径
```

## 配置系统架构

### 配置分层

1. **默认配置** (DefaultConfig.cs)：各模块提供默认值字典
2. **持久化配置** (%AppData%/TMSpeech/config.json)：用户修改的配置
3. **运行时配置** (ConfigManager)：内存中的配置状态

### 配置键命名规范

- 通用配置：`{section}.{key}` 例如 `general.StartOnLaunch`
- 插件配置：`plugin.{moduleId}!{pluginGuid}.config`

### 配置变更通知

```
ConfigManager.Apply(key, value)
    ↓
ConfigManager.ConfigChanged 事件触发
    ↓
[MainViewModel:47-57] GetPropObservable() 订阅特定键的变更
    ↓
ReactiveUI 自动更新绑定属性
    ↓
UI 自动刷新
```

## 数据流总览

```
[用户操作]
    ↓
[Avalonia UI - TMSpeech.GUI]
    ↓ Command
[ViewModel - ReactiveUI]
    ↓ 调用
[JobManager - TMSpeech.Core]
    ↓ 协调
[AudioSource Plugin] ──数据──→ [Recognizer Plugin]
    ↓ 事件                        ↓ 事件
[JobManager]
    ↓ 事件
[ViewModel]
    ↓ 数据绑定
[UI 更新]
```

## 技术栈总结

| 层次 | 技术 | 用途 |
|------|------|------|
| UI 框架 | Avalonia 11 | 跨平台桌面应用界面 |
| MVVM | ReactiveUI | 响应式编程和数据绑定 |
| 音频采集 | NAudio (WASAPI) | Windows 音频捕获 |
| 语音识别 | sherpa-onnx | 离线语音识别引擎 |
| 插件系统 | AssemblyLoadContext | 动态程序集加载和隔离 |
| 配置管理 | System.Text.Json | JSON 序列化/反序列化 |
| 资源下载 | Downloader | 异步文件下载 |
| 压缩解压 | SharpCompress | 处理插件/模型压缩包 |
| 构建优化 | NetBeauty | 整理发布目录结构 |

## 扩展开发指南

### 开发新的音频源插件

1. 创建类库项目，引用 TMSpeech.Core
2. 实现 `IAudioSource` 接口
3. 实现 `IPluginConfigEditor` 用于配置界面
4. 创建 `tmmodule.json` 描述插件信息
5. 编译到 plugins/[PluginName] 目录

示例：`TMSpeech.AudioSource.Windows/MicrophoneAudioSource.cs`

### 开发新的识别器插件

1. 创建类库项目，引用 TMSpeech.Core
2. 实现 `IRecognizer` 接口
3. 实现 Feed() 方法接收音频数据
4. 在后台线程处理识别，通过事件发出结果
5. 实现配置编辑器和模块描述

示例：`TMSpeech.Recognizer.SherpaOnnx/SherpaOnnxRecognizer.cs`

### 插件开发注意事项

- 插件必须避免引用 TMSpeech.GUI 或 TMSpeech 项目
- 只能依赖 TMSpeech.Core 提供的接口
- 必须实现 IPlugin.Available 属性检查运行环境
- 异常应通过 ExceptionOccured 事件通知宿主
- 配置字符串由插件自行序列化/反序列化（通常使用 JSON）
