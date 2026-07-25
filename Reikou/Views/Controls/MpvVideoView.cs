using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Banned.Avalonia.Danmaku.Playback;
using Microsoft.Extensions.Logging.Abstractions;
using Reikou.Models.Enums;
using Reikou.Services;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Reikou.Views.Controls;

public class MpvVideoView : OpenGlControlBase, IDisposable
{
    private readonly TaskCompletionSource<bool> _glInitSignal = new();

    private MpvService?         _mpvService;
    private MpvPlaybackSession? _playbackSession;
    private int                 _frameRequestPending;

#if DEBUG
    private long _renderSampleStarted = Stopwatch.GetTimestamp();
    private long _renderElapsedTicks;
    private int  _renderedFrameCount;
    private int  _framebufferSizeChangeCount;
    private int  _lastFramebufferWidth;
    private int  _lastFramebufferHeight;
    private int  _renderUpdateCallbackCount;
    private int  _coalescedFrameRequestCount;
#endif

    public event EventHandler<string>? FileLoaded;

    public MpvService?       Service         => _mpvService;
    public IPlaybackSession? PlaybackSession => _playbackSession;

    public MpvPlaybackState PlaybackState => _mpvService?.PlaybackState ?? MpvPlaybackState.Default;
    public double           Duration      => PlaybackState.Duration;
    public double           Position      => PlaybackState.Position;
    public bool             IsPlaying     => !PlaybackState.IsPaused;

    public MpvVideoView()
    {
        _mpvService      = new MpvService(NullLogger<MpvService>.Instance);
        _playbackSession = new MpvPlaybackSession(_mpvService);

        _mpvService.SetProperty("input-default-bindings", false);
        _mpvService.SetProperty("osc", false);
        _mpvService.SetProperty("osd-bar", false);
        _mpvService.SetProperty("video-sync", "audio");
        _mpvService.SetProperty("interpolation", false);
        _mpvService.SetProperty("render-context-block-for-target-time", false);
        _mpvService.SetProperty("opengl-swapinterval", 0);
        _mpvService.SetProperty("video-latency-hacks", true);
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        _mpvService?.InitializeOpenGl(getProcAddress : gl.GetProcAddress, updateCallback : QueueNextFrameRendering);

        _glInitSignal.TrySetResult(true);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        // Keep callbacks coalesced until this render actually starts. Clearing the
        // flag in the dispatcher callback can enqueue several renders before the
        // compositor has consumed the first request.
        Interlocked.Exchange(ref _frameRequestPending, 0);

        if (_mpvService == null) return;

        var scaling   = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        var pixelSize = PixelSize.FromSize(Bounds.Size, scaling);
        var width     = pixelSize.Width;
        var height    = pixelSize.Height;

        if (width <= 0 || height <= 0) return;

        _mpvService.UpdateRenderState();

#if DEBUG
        var renderStarted = Stopwatch.GetTimestamp();
#endif
        _mpvService.Render(fb, width, height);
#if DEBUG
        RecordRenderSample(renderStarted, width, height);
#endif
    }

    private void QueueNextFrameRendering()
    {
#if DEBUG
        Interlocked.Increment(ref _renderUpdateCallbackCount);
#endif
        if (Interlocked.Exchange(ref _frameRequestPending, 1) == 1)
        {
#if DEBUG
            Interlocked.Increment(ref _coalescedFrameRequestCount);
#endif
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_mpvService == null)
            {
                Interlocked.Exchange(ref _frameRequestPending, 0);
                return;
            }

            RequestNextFrameRendering();
        }, DispatcherPriority.Render);
    }

#if DEBUG
    private void RecordRenderSample(long renderStarted, int width, int height)
    {
        var now = Stopwatch.GetTimestamp();
        _renderElapsedTicks += now - renderStarted;
        _renderedFrameCount++;

        if (width != _lastFramebufferWidth || height != _lastFramebufferHeight)
        {
            _framebufferSizeChangeCount++;
            _lastFramebufferWidth  = width;
            _lastFramebufferHeight = height;
        }

        if (now - _renderSampleStarted < Stopwatch.Frequency)
        {
            return;
        }

        var renderMilliseconds  = _renderElapsedTicks                               * 1000.0 / Stopwatch.Frequency;
        var averageMilliseconds = _renderedFrameCount == 0 ? 0 : renderMilliseconds / _renderedFrameCount;
        var updateCallbacks     = Interlocked.Exchange(ref _renderUpdateCallbackCount, 0);
        var coalescedRequests   = Interlocked.Exchange(ref _coalescedFrameRequestCount, 0);
        Debug.WriteLine(
                        $"[MpvRender] frames={_renderedFrameCount}; total={renderMilliseconds:0.00}ms; " +
                        $"avg={averageMilliseconds:0.00}ms; fbo={width}x{height}; "                      +
                        $"fbo changes={_framebufferSizeChangeCount}; callbacks={updateCallbacks}; "      +
                        $"coalesced={coalescedRequests}"
                       );

        _renderSampleStarted        = now;
        _renderElapsedTicks         = 0;
        _renderedFrameCount         = 0;
        _framebufferSizeChangeCount = 0;
    }
#endif

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        Interlocked.Exchange(ref _frameRequestPending, 0);
        base.OnOpenGlDeinit(gl);
        Dispose();
    }

    public async Task LoadFileAsync(string url)
    {
        await _glInitSignal.Task;
        if (_mpvService == null || _playbackSession == null)
        {
            return;
        }

        _playbackSession.OpenMedia(url);
        var status = _mpvService.Command("loadfile", url, "replace");
        if ((int)status < 0)
        {
            _playbackSession.FailCurrentMedia($"mpv failed to open '{url}'.");
            return;
        }

        _mpvService.SetProperty("pause", false);

        var fileName = System.IO.Path.GetFileNameWithoutExtension(url);
        FileLoaded?.Invoke(this, fileName);
    }

    public async Task LoadSubtitleAsync(string url)
    {
        await _glInitSignal.Task;
        _mpvService?.Command("sub-add", url, "select");
    }

    public void TakeScreenshot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        _mpvService?.Command("screenshot-to-file", path, "window");
    }

    public void TogglePause() => _mpvService?.Command("cycle", "pause");

    public MpvStatus SelectAudioTrack(long trackId) =>
        _mpvService?.SelectAudioTrack(trackId) ?? MpvStatus.Uninitialized;

    public MpvStatus DisableAudioTrack() => _mpvService?.DisableAudioTrack() ?? MpvStatus.Uninitialized;

    public MpvStatus SelectSubtitleTrack(long trackId) =>
        _mpvService?.SelectSubtitleTrack(trackId) ?? MpvStatus.Uninitialized;

    public MpvStatus DisableSubtitleTrack() => _mpvService?.DisableSubtitleTrack() ?? MpvStatus.Uninitialized;

    public MpvStatus SelectSecondarySubtitleTrack(long trackId) =>
        _mpvService?.SelectSecondarySubtitleTrack(trackId) ?? MpvStatus.Uninitialized;

    public MpvStatus DisableSecondarySubtitleTrack() =>
        _mpvService?.DisableSecondarySubtitleTrack() ?? MpvStatus.Uninitialized;

    public MpvStatus SetSubtitleVisibility(bool isVisible) =>
        _mpvService?.SetSubtitleVisibility(isVisible) ?? MpvStatus.Uninitialized;

    public MpvStatus SetSubtitleDelay(double seconds) =>
        _mpvService?.SetSubtitleDelay(seconds) ?? MpvStatus.Uninitialized;

    public MpvStatus ResetSubtitleDelay() => _mpvService?.ResetSubtitleDelay() ?? MpvStatus.Uninitialized;

    public MpvStatus SetAudioDelay(double seconds) => _mpvService?.SetAudioDelay(seconds) ?? MpvStatus.Uninitialized;

    public MpvStatus ResetAudioDelay() => _mpvService?.ResetAudioDelay() ?? MpvStatus.Uninitialized;

    public void BeginSeek(double seconds)
    {
        if (double.IsFinite(seconds))
        {
            _playbackSession?.BeginSeek(TimeSpan.FromSeconds(Math.Max(0, seconds)));
        }
    }

    public void Seek(double seconds) => ExecuteSeek(Math.Max(0, seconds), Math.Max(0, seconds), "absolute");

    public void SeekFast(double seconds) =>
        ExecuteSeek(Math.Max(0, seconds), Math.Max(0, seconds), "absolute+keyframes");

    public void SeekRelative(double seconds)
    {
        var target = PlaybackState.Position + seconds;
        ExecuteSeek(target, seconds, "relative");
    }

    public void SeekRelativeFast(double seconds)
    {
        var target = PlaybackState.Position + seconds;
        ExecuteSeek(target, seconds, "relative+keyframes");
    }

    public void Stop(bool clearMedia = false)
    {
        if (clearMedia)
        {
            _playbackSession?.ClearMedia();
        }

        _mpvService?.Command("stop");
    }

    private void ExecuteSeek(double requestedSeconds, double commandSeconds, string mode)
    {
        if (_mpvService      == null           ||
            _playbackSession == null           ||
            !double.IsFinite(requestedSeconds) ||
            !double.IsFinite(commandSeconds))
        {
            return;
        }

        var requestedPosition = TimeSpan.FromSeconds(Math.Max(0, requestedSeconds));
        _playbackSession.BeginSeek(requestedPosition);
        var status = _mpvService.Command("seek", commandSeconds.ToString("R", CultureInfo.InvariantCulture), mode);
        if ((int)status < 0)
        {
            _playbackSession.CancelSeek();
        }
    }

    public void RefreshVideoFrame()
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            RequestNextFrameRendering();
            return;
        }

        Dispatcher.UIThread.Post(RequestNextFrameRendering, DispatcherPriority.Render);
    }

    public void Dispose()
    {
        _playbackSession?.Dispose();
        _playbackSession = null;
        _mpvService?.Dispose();
        _mpvService = null;
        GC.SuppressFinalize(this);
    }
}
