using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Threading.Tasks;
using TestMpv.Services;

namespace TestMpv;

public class MpvVideoView : OpenGlControlBase, IDisposable
{
    private          MpvService?                _mpvService;
    private readonly TaskCompletionSource<bool> _glInitSignal = new();

    public MpvService? Service   => _mpvService;
    public double      Duration  => _mpvService?.GetProperty<double>("duration") ?? 0;
    public double      Position  => _mpvService?.GetProperty<double>("time-pos") ?? 0;
    public bool        IsPlaying => !(_mpvService?.GetProperty<bool>("pause") ?? true);

    public MpvVideoView()
    {
        _mpvService = new MpvService(NullLogger<MpvService>.Instance);

        _mpvService.SetProperty("input-default-bindings", false);
        _mpvService.SetProperty("osc", false);
        _mpvService.SetProperty("osd-bar", false);
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        _mpvService?.InitializeOpenGl(
                                      getProcAddress : gl.GetProcAddress,
                                      updateCallback : () =>
                                          Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Render)
                                     );

        _glInitSignal.TrySetResult(true);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_mpvService == null) return;

        var scaling = VisualRoot?.RenderScaling ?? 1.0;
        var width   = (int)(Bounds.Width  * scaling);
        var height  = (int)(Bounds.Height * scaling);

        _mpvService.Render(fb, width, height);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        base.OnOpenGlDeinit(gl);
        Dispose();
    }

    public async Task LoadFileAsync(string url)
    {
        await _glInitSignal.Task;
        var safeUrl = url.Replace("\\", "/");
        _mpvService?.Command($"loadfile \"{safeUrl}\" replace");
    }

    public void TogglePause()            => _mpvService?.Command("cycle pause");
    public void Seek(double     seconds) => _mpvService?.Command($"seek {seconds} absolute");
    public void SeekFast(double seconds) => _mpvService?.Command($"seek {seconds} absolute+keyframes");

    public void Dispose()
    {
        _mpvService?.Dispose();
        _mpvService = null;
        GC.SuppressFinalize(this);
    }
}
