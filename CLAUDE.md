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
                ├─→ TMSpeech.Recognizer.SherpaOnnx
                └─→ TMSpeech.Recognizer.Command
```

## 插件实现详解

### TMSpeech.Recognizer.Command (命令行识别器)

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
| `tmmodule.json` | 模块元数据，定义插件 ID、版本、名称、描述等信息 |
| `TMSpeech.Recognizer.Command.csproj` | .NET 6.0 项目文件，引用 TMSpeech.Core 核心库 |
| `CommandRecognizer.cs` | 识别器主实现文件 |
| `CommandRecognizerConfigEditor.cs` | 配置编辑器实现文件 |

**CommandRecognizer.cs 详解**：

1. **CommandRecognizerConfig (8-14行)**
   - 配置数据类
   - 属性：Command (命令路径)、Arguments (参数)、WorkingDirectory (工作目录)、LogFile (日志文件)

2. **CommandRecognizer (16-268行)**
   - 实现 `IRecognizer` 接口
   - GUID: `A1B2C3D4-5E6F-7890-ABCD-EF1234567890`

   **主要方法**：
   - `LoadConfig (45-58行)`: 从 JSON 字符串反序列化配置
   - `Start (76-156行)`: 启动外部命令进程
     - 配置 ProcessStartInfo，重定向标准输出和错误流
     - 创建日志文件目录和 StreamWriter (92-112行)
     - 启动两个后台线程：输出读取线程和错误读取线程 (135-149行)
   - `Stop (158-187行)`: 停止进程，清理资源
     - 终止进程、释放线程、关闭日志文件
   - `ReadOutputLoop (189-244行)`: 标准输出读取循环
     - 逐字符读取标准输出 (198行)
     - 处理 `\r`：清空缓冲，触发 TextChanged 事件 (205-216行)
     - 处理 `\n`：清空缓冲，触发 SentenceDone 事件 (217-228行)
     - 其他字符：追加到当前行缓冲 (229-233行)
   - `ReadErrorLoop (246-267行)`: 标准错误读取循环
     - 读取 stderr 并写入日志文件 (253-260行)
   - `Feed (70-74行)`: 空实现，命令行识别器不使用此方法

   **线程安全**：
   - 使用 `_lockObject` 保护 `_currentLine` 的并发访问 (203行)
   - 使用 `_isRunning` 标志控制线程生命周期

**CommandRecognizerConfigEditor.cs 详解**：

1. **CommandRecognizerConfigEditor (6-103行)**
   - 实现 `IPluginConfigEditor` 接口
   - 使用字典存储配置值 `_values` (8行)
   - 使用列表存储表单项定义 `_formItems` (9行)

   **构造函数 (14-47行)**：
   - 初始化4个配置项的默认值
   - 定义4个表单项：
     - Command: 文件选择器 (21-26行)
     - Arguments: 文本框 (28-32行)
     - WorkingDirectory: 文件夹选择器 (34-39行)
     - LogFile: 文件选择器 (41-46行)

   **主要方法**：
   - `GetFormItems (49-52行)`: 返回只读表单项列表
   - `SetValue/GetValue (59-68行)`: 配置值的读写
   - `GenerateConfig (70-81行)`: 将配置值序列化为 JSON
   - `LoadConfigString (83-102行)`: 从 JSON 反序列化配置到字典
