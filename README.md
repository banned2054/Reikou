# Reikou

Reikou is a desktop video player built with Avalonia UI, OpenGL, and `libmpv`. It uses a custom `OpenGlControlBase` host to render `mpv` video frames inside an Avalonia application while keeping the player UI fully managed in .NET.

The project currently targets Windows and focuses on a lightweight local playback experience with playlist navigation, subtitle loading, screenshots, and a clean overlay-based control surface.

## Features

- Avalonia desktop UI with a custom OpenGL-backed `libmpv` video surface
- Local video playback powered by `libmpv`
- Drag and drop support for opening media files and subtitles
- Subtitle loading for `.ass`, `.srt`, `.vtt`, and `.sub`
- Auto-generated playlist from the current video's directory
- Previous / next navigation and relative seeking
- Play / pause, volume, playback speed, and fullscreen controls
- Screenshot capture to the user's Pictures folder
- Overlay controls that auto-hide when the pointer is idle
- Mouse and keyboard interactions for quick playback control

## Tech Stack

- .NET `10.0`
- Avalonia `11.3.9`
- OpenGL via `Avalonia.OpenGL.Controls`
- `libmpv` native playback/rendering
- Serilog for logging

## Supported Formats

### Video

- `.mp4`
- `.mkv`
- `.avi`
- `.mov`
- `.flv`

### Subtitle

- `.ass`
- `.srt`
- `.vtt`
- `.sub`

## How It Works

Reikou wraps `libmpv` through P/Invoke and creates an mpv render context with the `opengl` backend. The custom [`MpvVideoView`](./Reikou/Views/Controls/MpvVideoView.cs) control inherits from Avalonia's `OpenGlControlBase`, initializes the mpv OpenGL renderer, and forwards each frame into the current framebuffer.

The window and overlay UI stay in Avalonia, while playback state is synchronized from mpv on a timer. This keeps the rendering path efficient without giving up normal Avalonia input and layout behavior.

## Project Structure

```text
Reikou/
|- Reikou/Views/Controls/MpvVideoView.cs      # OpenGL host for libmpv rendering
|- Reikou/Services/MpvService.cs              # mpv lifecycle, commands, properties
|- Reikou/Views/Pages/MainWindow.axaml*       # Main player window and interaction logic
|- Reikou/Views/Components/*                  # Overlay and transport controls
|- Reikou/ViewModels/*                        # UI state and playlist handling
|- Reikou/Native/LibMpv.cs                    # Native interop bindings
|- Reikou/Libs/libmpv-2.dll                   # Bundled Windows mpv runtime
```

## Running The Project

### Prerequisites

- Windows x64
- .NET SDK 10
- OpenGL-capable graphics driver

`libmpv-2.dll` is already included under `Reikou/Libs`, and the application resolves it at startup from the output directory.

### Run in development

```powershell
dotnet run --project .\Reikou\Reikou.csproj
```

### Build

```powershell
dotnet build .\Reikou\Reikou.csproj
```

### Publish

The Release configuration is set up for `win-x64`, self-contained publishing, trimming, and native AOT:

```powershell
dotnet publish .\Reikou\Reikou.csproj -c Release
```

## User Interaction

- Drag a video file into the window to open it
- Drag a subtitle file into the window to attach subtitles to the current video
- Press `Space` to toggle play/pause
- Press `Left` / `Right` for quick seek
- Double-click the video area to toggle play/pause
- Press and hold on the video area to temporarily play at `2x`
- Use the overlay controls for volume, speed, screenshots, and fullscreen

When a video is opened, Reikou can automatically scan the same directory and build a playlist from neighboring video files.

## Logging

The app writes logs to:

```text
logs/myapp-*.log
```

This is configured in [`Program.cs`](./Reikou/Program.cs).

## Current Notes

- The current repository is Windows-focused because it ships a Windows `libmpv` binary and Release publishing is configured for `win-x64`
- Danmaku UI hooks exist, but the actual danmaku implementation is still marked as TODO
- Screenshot output is currently saved under the user's `Pictures\TestMpv` directory

## Chinese Documentation

The Chinese version of this document is available at [`Docs/README.zh-CN.md`](./Docs/README.zh-CN.md).
