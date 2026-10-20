using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Banned.Avalonia.Danmaku.Services.Playback;
using Reikou.Models.Enums;
using Reikou.Models.Settings;
using Reikou.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Reikou.Presentation.Views.Player;

public class MpvVideoView : OpenGlControlBase, IDisposable
{
    private readonly TaskCompletionSource<bool> _glInitSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _frameRequestPending;

    public MpvVideoView()
    {
        var appSettings = AppSettingsStore.Current.Settings;
        Service = new MpvService(new SerilogLogger<MpvService>(), MpvInitializationOptions.From(appSettings));
        NativePlaybackSession = new MpvPlaybackSession(Service);

        ApplyRuntimeSettings(appSettings);
    }

    public MpvService? Service { get; private set; }

    public   IPlaybackSession?   PlaybackSession       => NativePlaybackSession;
    internal MpvPlaybackSession? NativePlaybackSession { get; private set; }

    public MpvPlaybackState PlaybackState => Service?.PlaybackState ?? MpvPlaybackState.Default;

    public double Duration  => PlaybackState.Duration;
    public double Position  => PlaybackState.Position;
    public bool   IsPlaying => !PlaybackState.IsPaused;

    public void Dispose()
    {
        _glInitSignal.TrySetCanceled();
        NativePlaybackSession?.Dispose();
        NativePlaybackSession = null;
        Service?.Dispose();
        Service = null;
        GC.SuppressFinalize(this);
    }

    internal async Task LoadNativeFileAsync(string path, CancellationToken cancellationToken)
    {
        var revision = NativePlaybackSession?.MediaRevision;
        await _glInitSignal.Task.WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (Service == null || NativePlaybackSession == null) throw new ObjectDisposedException(nameof(MpvVideoView));

        if (revision != NativePlaybackSession.MediaRevision) throw new OperationCanceledException(cancellationToken);

        var status = Service.LoadMedia(path, revision.Value);
        if ((int)status < 0) throw new InvalidOperationException($"mpv failed to open '{path}': {status}.");

        Service.SetProperty("pause", false);
    }

    public async Task<MpvStatus> LoadSubtitleAsync(string path, CancellationToken cancellationToken = default)
    {
        await _glInitSignal.Task.WaitAsync(cancellationToken);
        return Service?.Command("sub-add", path, "select") ?? MpvStatus.Uninitialized;
    }

    public MpvStatus TakeScreenshot(string? path = null) => path == null
        ? Service?.Command("screenshot", "window")               ?? MpvStatus.Uninitialized
        : Service?.Command("screenshot-to-file", path, "window") ?? MpvStatus.Uninitialized;

    public void TogglePause() => Service?.Command("cycle", "pause");

    public MpvStatus SelectAudioTrack(long trackId) => Service?.SelectAudioTrack(trackId) ?? MpvStatus.Uninitialized;

    public MpvStatus DisableAudioTrack() => Service?.DisableAudioTrack() ?? MpvStatus.Uninitialized;

    public MpvStatus SelectSubtitleTrack(long trackId) =>
        Service?.SelectSubtitleTrack(trackId) ?? MpvStatus.Uninitialized;

    public MpvStatus DisableSubtitleTrack() => Service?.DisableSubtitleTrack() ?? MpvStatus.Uninitialized;

    public MpvStatus SelectSecondarySubtitleTrack(long trackId) =>
        Service?.SelectSecondarySubtitleTrack(trackId) ?? MpvStatus.Uninitialized;

    public MpvStatus DisableSecondarySubtitleTrack() =>
        Service?.DisableSecondarySubtitleTrack() ?? MpvStatus.Uninitialized;

    public MpvStatus SetSubtitleVisibility(bool isVisible) =>
        Service?.SetSubtitleVisibility(isVisible) ?? MpvStatus.Uninitialized;

    public MpvStatus SetSubtitleDelay(double seconds) => Service?.SetSubtitleDelay(seconds) ?? MpvStatus.Uninitialized;

    public MpvStatus ResetSubtitleDelay() => Service?.ResetSubtitleDelay() ?? MpvStatus.Uninitialized;

    public MpvStatus SetAudioDelay(double seconds) => Service?.SetAudioDelay(seconds) ?? MpvStatus.Uninitialized;

    public MpvStatus ResetAudioDelay() => Service?.ResetAudioDelay() ?? MpvStatus.Uninitialized;

    public void ApplyRuntimeSettings(AppSettings settings)
    {
        if (Service == null) return;

        foreach (var option in MpvSettingsMapper.GetOptions(settings))
            if (!MpvSettingsMapper.RequiresRestart(option.Key))
                Service.SetProperty(option.Key, option.Value);
    }

    public void ApplyRuntimeSettings(AppSettings previousSettings, AppSettings currentSettings)
    {
        if (Service == null) return;

        var previous = new Dictionary<string, string>(MpvSettingsMapper.GetOptions(previousSettings),
                                                      StringComparer.Ordinal);
        var current = new Dictionary<string, string>(MpvSettingsMapper.GetOptions(currentSettings),
                                                     StringComparer.Ordinal);

        foreach (var optionName in previous.Keys)
            if (!current.ContainsKey(optionName))
                current[optionName] = string.Empty;

        foreach (var option in current)
            if (!MpvSettingsMapper.RequiresRestart(option.Key) &&
                (!previous.TryGetValue(option.Key, out var previousValue) ||
                 !string.Equals(previousValue, option.Value, StringComparison.Ordinal)))
                Service.SetProperty(option.Key, option.Value);
    }

    public void BeginSeek(double seconds)
    {
        if (double.IsFinite(seconds)) NativePlaybackSession?.BeginSeek(TimeSpan.FromSeconds(Math.Max(0, seconds)));
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
        if (clearMedia) NativePlaybackSession?.ClearMedia();

        Service?.Command("stop");
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

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        Service?.InitializeOpenGl(gl.GetProcAddress, QueueNextFrameRendering);

        _glInitSignal.TrySetResult(true);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        // Keep callbacks coalesced until this render actually starts. Clearing the
        // flag in the dispatcher callback can enqueue several renders before the
        // compositor has consumed the first request.
        Interlocked.Exchange(ref _frameRequestPending, 0);

        if (Service == null) return;

        var scaling   = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        var pixelSize = PixelSize.FromSize(Bounds.Size, scaling);
        var width     = pixelSize.Width;
        var height    = pixelSize.Height;

        if (width <= 0 || height <= 0) return;

        Service.UpdateRenderState();

#if DEBUG
        var renderStarted = Stopwatch.GetTimestamp();
#endif
        Service.Render(fb, width, height);
#if DEBUG
        RecordRenderSample(renderStarted, width, height);
#endif
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        Interlocked.Exchange(ref _frameRequestPending, 0);
        base.OnOpenGlDeinit(gl);
        Dispose();
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
            if (Service == null)
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

        if (now - _renderSampleStarted < Stopwatch.Frequency) return;

        var renderMilliseconds  = _renderElapsedTicks                               * 1000.0 / Stopwatch.Frequency;
        var averageMilliseconds = _renderedFrameCount == 0 ? 0 : renderMilliseconds / _renderedFrameCount;
        var updateCallbacks     = Interlocked.Exchange(ref _renderUpdateCallbackCount, 0);
        var coalescedRequests   = Interlocked.Exchange(ref _coalescedFrameRequestCount, 0);
        Debug.WriteLine($"[MpvRender] frames={_renderedFrameCount}; total={renderMilliseconds:0.00}ms; " +
                        $"avg={averageMilliseconds:0.00}ms; fbo={width}x{height}; "                      +
                        $"fbo changes={_framebufferSizeChangeCount}; callbacks={updateCallbacks}; "      +
                        $"coalesced={coalescedRequests}");

        _renderSampleStarted        = now;
        _renderElapsedTicks         = 0;
        _renderedFrameCount         = 0;
        _framebufferSizeChangeCount = 0;
    }
#endif

    private void ExecuteSeek(double requestedSeconds, double commandSeconds, string mode)
    {
        if (Service               == null      ||
            NativePlaybackSession == null      ||
            !double.IsFinite(requestedSeconds) ||
            !double.IsFinite(commandSeconds))
            return;

        var requestedPosition = TimeSpan.FromSeconds(Math.Max(0, requestedSeconds));
        NativePlaybackSession.BeginSeek(requestedPosition);
        var status = Service.Command("seek", commandSeconds.ToString("R", CultureInfo.InvariantCulture), mode);
        if ((int)status < 0) NativePlaybackSession.CancelSeek();
    }

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
}
