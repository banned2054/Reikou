# Reikou

Reikou 是一个基于 Avalonia UI、OpenGL 和 `libmpv` 的桌面视频播放器。它通过自定义的 `OpenGlControlBase` 宿主，把 `mpv` 的视频帧渲染到 Avalonia 应用中，同时播放器界面仍然完全由 .NET 管理。

当前项目以 Windows 为主要目标平台，重点提供轻量的本地播放体验，包括播放列表导航、字幕加载、截图，以及基于浮层的控制界面。

## 功能特性

- 基于 Avalonia 桌面 UI，使用自定义 OpenGL 视频控件承载 `libmpv`
- 使用 `libmpv` 实现本地视频播放
- 支持拖拽打开媒体文件和字幕文件
- 支持加载 `.ass`、`.srt`、`.vtt`、`.sub` 字幕
- 可根据当前视频所在目录自动生成播放列表
- 支持上一项 / 下一项切换与相对跳转
- 支持播放 / 暂停、音量、倍速、全屏控制
- 支持截图并保存到用户图片目录
- 浮层控制条会在鼠标静止时自动隐藏
- 提供适合快速操作的键盘和鼠标交互

## 技术栈

- .NET `10.0`
- Avalonia `11.3.9`
- 基于 `Avalonia.OpenGL.Controls` 的 OpenGL 渲染
- `libmpv` 原生播放与渲染
- Serilog 日志

## 支持格式

### 视频

- `.mp4`
- `.mkv`
- `.avi`
- `.mov`
- `.flv`

### 字幕

- `.ass`
- `.srt`
- `.vtt`
- `.sub`

## 工作原理

Reikou 通过 P/Invoke 封装 `libmpv`，并使用 `opengl` 后端创建 mpv 的 render context。自定义控件 [`MpvVideoView`](../Reikou/Views/Controls/MpvVideoView.cs) 继承自 Avalonia 的 `OpenGlControlBase`，负责初始化 mpv 的 OpenGL 渲染器，并将每一帧绘制到当前 framebuffer 中。

窗口与浮层界面仍然由 Avalonia 负责，而播放状态则通过定时器从 mpv 同步回来。这样既保留了高效的视频渲染路径，也保留了 Avalonia 常规的输入与布局能力。

## 项目结构

```text
Reikou/
|- Reikou/Views/Controls/MpvVideoView.cs      # libmpv 的 OpenGL 视频宿主
|- Reikou/Services/MpvService.cs              # mpv 生命周期、命令与属性读写
|- Reikou/Views/Pages/MainWindow.axaml*       # 主窗口与交互逻辑
|- Reikou/Views/Components/*                  # 浮层与传输控制组件
|- Reikou/ViewModels/*                        # UI 状态与播放列表处理
|- Reikou/Native/LibMpv.cs                    # 原生互操作绑定
|- Reikou/Libs/libmpv-2.dll                   # 内置的 Windows mpv 运行库
```

## 运行项目

### 前置要求

- Windows x64
- .NET SDK 10
- 支持 OpenGL 的显卡驱动

仓库已在 `Reikou/Libs` 下包含 `libmpv-2.dll`，程序启动时会从输出目录自动解析并加载该库。

### 开发运行

```powershell
dotnet run --project .\Reikou\Reikou.csproj
```

### 构建

```powershell
dotnet build .\Reikou\Reikou.csproj
```

### 发布

Release 配置已经针对 `win-x64`、自包含发布、裁剪和 Native AOT 做了设置：

```powershell
dotnet publish .\Reikou\Reikou.csproj -c Release
```

## 使用方式

- 将视频文件拖入窗口即可播放
- 将字幕文件拖入窗口即可为当前视频加载字幕
- 按 `Space` 切换播放 / 暂停
- 按 `Left` / `Right` 进行快速跳转
- 双击视频区域切换播放 / 暂停
- 在视频区域长按可临时切换到 `2x` 播放
- 可通过浮层控制条调节音量、倍速、截图和全屏

当打开一个视频后，Reikou 可以自动扫描其所在目录，并将同目录中的其他视频加入播放列表。

## 日志

程序会将日志写入：

```text
logs/myapp-*.log
```

相关配置位于 [`Program.cs`](../Reikou/Program.cs)。

## 当前说明

- 当前仓库偏向 Windows 平台，因为内置的是 Windows 版 `libmpv`，且 Release 发布目标为 `win-x64`
- 已经预留弹幕相关 UI 钩子，但实际弹幕功能仍然是 TODO
- 截图当前会保存到用户的 `Pictures\TestMpv` 目录

## English Documentation

英文版文档位于 [`README.md`](../README.md)。
