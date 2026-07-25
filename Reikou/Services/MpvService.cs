using Microsoft.Extensions.Logging;
using Reikou.Models;
using Reikou.Models.Enums;
using Reikou.Models.Structs;
using Reikou.Native;
using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Reikou.Services;

public class MpvService : IDisposable
{
    private const ulong DurationObserverId               = 1;
    private const ulong PositionObserverId               = 2;
    private const ulong PauseObserverId                  = 3;
    private const ulong SpeedObserverId                  = 4;
    private const ulong VolumeObserverId                 = 5;
    private const ulong VideoWidthObserverId             = 6;
    private const ulong VideoHeightObserverId            = 7;
    private const ulong TrackListObserverId              = 8;
    private const ulong AudioTrackObserverId             = 9;
    private const ulong SubtitleTrackObserverId          = 10;
    private const ulong SecondarySubtitleTrackObserverId = 11;
    private const ulong SubtitleDelayObserverId          = 12;
    private const ulong AudioDelayObserverId             = 13;
    private const ulong SubtitleVisibilityObserverId     = 14;

    private readonly ILogger<MpvService> _logger;

    private nint _handle;
    private nint _renderContext;
    private bool _disposed;

    private MpvPlaybackState         _playbackState = MpvPlaybackState.Default;
    private CancellationTokenSource? _eventLoopCts;
    private Task?                    _eventLoopTask;

    // 防止委托被 GC 回收
    private LibMpv.MpvGetProcAddressFn? _getProcAddressDelegate;
    private LibMpv.MpvRenderUpdateFn?   _updateCallbackDelegate;

    public MpvService(ILogger<MpvService> logger)
    {
        _logger = logger;
        _handle = LibMpv.Create();
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create mpv instance.");

        var status = LibMpv.Initialize(_handle);
        if (status < 0)
            HandleError(status, "Initialize failed");

        StartPlaybackStateObservation();
    }

    public MpvPlaybackState PlaybackState => Volatile.Read(ref _playbackState);

    public event EventHandler<MpvPlaybackStateChangedEventArgs>? PlaybackStateChanged;
    public event EventHandler<MpvPlaybackEventArgs>?             PlaybackEvent;

    public void InitializeOpenGl(Func<string, nint> getProcAddress, Action updateCallback)
    {
        CheckDisposed();

        // 1. 包装委托
        _getProcAddressDelegate = (ctx, name) => getProcAddress(name);
        // mpv invokes this from an arbitrary thread. Keep it as a wake-up only;
        // mpv_render_context_update is consumed on Avalonia's GL render thread.
        _updateCallbackDelegate = _ => updateCallback();

        // 2. 准备初始化参数 (使用 Marshal 分配内存，这是原版最稳妥的做法)
        var glInitParams = new MpvOpenglInitParams
        {
            getProcAddress        = _getProcAddressDelegate,
            getProcAddressContext = nint.Zero
        };

        var glInitParamsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(glInitParams));
        Marshal.StructureToPtr(glInitParams, glInitParamsPtr, false);

        // 3. 准备 API 字符串
        var apiBytes = LibMpv.GetUtf8Bytes("opengl");
        var apiPtr   = Marshal.AllocHGlobal(apiBytes.Length);
        Marshal.Copy(apiBytes, 0, apiPtr, apiBytes.Length);

        try
        {
            // 直接传递数组，让 LibraryImport 处理 Marshalling
            var renderParams = new MpvRenderParam[]
            {
                new() { type = MpvRenderParamType.ApiType, data          = apiPtr },
                new() { type = MpvRenderParamType.OpenglInitParams, data = glInitParamsPtr },
                new() { type = MpvRenderParamType.Invalid, data          = nint.Zero }
            };

            var err = LibMpv.RenderContextCreate(out _renderContext, _handle, renderParams);
            if (err < 0)
            {
                throw new Exception($"Failed to create mpv render context: {LibMpv.GetError(err)}");
            }
        }
        finally
        {
            // 清理手动分配的非托管内存
            Marshal.FreeHGlobal(glInitParamsPtr);
            Marshal.FreeHGlobal(apiPtr);
        }

        // 4. 设置回调
        LibMpv.RenderContextSetUpdateCallback(_renderContext, _updateCallbackDelegate, nint.Zero);
    }

    public MpvRenderUpdateFlag UpdateRenderState()
    {
        return _disposed || _renderContext == nint.Zero
            ? MpvRenderUpdateFlag.None
            : LibMpv.RenderContextUpdate(_renderContext);
    }

    public void Render(int fbo, int width, int height)
    {
        if (_disposed || _renderContext == IntPtr.Zero) return;

        var fboParam = new MpvOpenglFbo
        {
            fbo            = fbo,
            width          = width,
            heigh          = height,
            internalFormat = 0
        };

        int flipY = 1;

        // 这里原版使用了 unsafe + stackalloc，这部分通常是安全的，保留原版写法
        unsafe
        {
            var renderParams = stackalloc MpvRenderParam[4];

            renderParams[0].type = MpvRenderParamType.OpenglFbo;
            renderParams[0].data = (nint)(&fboParam);

            renderParams[1].type = MpvRenderParamType.FlipY;
            renderParams[1].data = (nint)(&flipY);

            renderParams[2].type = MpvRenderParamType.Invalid;
            renderParams[2].data = nint.Zero;

            var paramsSpan = new ReadOnlySpan<MpvRenderParam>(renderParams, 3);
            LibMpv.RenderContextRender(_renderContext, paramsSpan);
        }
    }

    public MpvStatus Command(params string[] arguments)
    {
        if (_disposed) return MpvStatus.Uninitialized;
        var err = LibMpv.Command(_handle, arguments);
        if (err < 0)
            HandleError(err, $"Command failed: {string.Join(" | ", arguments)}");
        return err;
    }

    public T? GetProperty<T>(string name)
    {
        CheckDisposed();

        // 1. 字符串类型处理
        if (typeof(T) == typeof(string))
        {
            var err = LibMpv.GetProperty(_handle, name, MpvFormat.String, out IntPtr lpBuffer);
            if (err < 0) return default;
            var result = LibMpv.ConvertFromUtf8(lpBuffer);
            LibMpv.Free(lpBuffer);
            return (T)(object)result;
        }

        // 2. 布尔类型处理 (这是修复的关键)
        // 必须显式使用 MpvFormat.Flag，否则 MPV 可能无法正确返回 pause 状态
        if (typeof(T) == typeof(bool))
        {
            var err = LibMpv.GetProperty(_handle, name, MpvFormat.Flag, out int val);
            if (err >= 0) return (T)(object)(val != 0);
            return default;
        }

        // 3. 整数/长整数处理
        if (typeof(T) == typeof(long) || typeof(T) == typeof(int))
        {
            var err = LibMpv.GetProperty(_handle, name, MpvFormat.Long, out long val);
            if (err < 0) return default;

            if (typeof(T) == typeof(int))
                return (T)(object)(int)val;
            return (T)(object)val;
        }

        // 4. 浮点数处理
        if (typeof(T) == typeof(double))
        {
            var err = LibMpv.GetProperty(_handle, name, MpvFormat.Double, out double val);
            if (err >= 0) return (T)(object)val;
            return default;
        }

        throw new NotSupportedException($"Type {typeof(T)} is not supported by MpvService.");
    }

    public MpvStatus SetProperty<T>(string name, T value)
    {
        CheckDisposed();
        MpvStatus err;

        switch (value)
        {
            case string s :
                err = LibMpv.SetProperty(_handle, name, MpvFormat.String, ref s);
                break;
            case bool b :
                var flag = b ? 1 : 0;
                err = LibMpv.SetProperty(_handle, name, MpvFormat.Flag, ref flag);
                break;
            case int i :
                long lInt = i;
                err = LibMpv.SetProperty(_handle, name, MpvFormat.Long, ref lInt);
                break;
            case long l :
                err = LibMpv.SetProperty(_handle, name, MpvFormat.Long, ref l);
                break;
            case double d :
                err = LibMpv.SetProperty(_handle, name, MpvFormat.Double, ref d);
                break;
            default :
                throw new NotSupportedException($"Type {typeof(T)} not supported");
        }

        if (err < 0) HandleError(err, $"SetProperty {name}={value}");
        return err;
    }

    public MpvStatus SelectAudioTrack(long trackId) => SetTrackProperty("aid", trackId);

    public MpvStatus DisableAudioTrack() => SetProperty("aid", "no");

    public MpvStatus SelectSubtitleTrack(long trackId) => SetTrackProperty("sid", trackId);

    public MpvStatus DisableSubtitleTrack() => SetProperty("sid", "no");

    public MpvStatus SelectSecondarySubtitleTrack(long trackId) => SetTrackProperty("secondary-sid", trackId);

    public MpvStatus DisableSecondarySubtitleTrack() => SetProperty("secondary-sid", "no");

    public MpvStatus SetSubtitleVisibility(bool isVisible) => SetProperty("sub-visibility", isVisible);

    public MpvStatus SetSubtitleDelay(double seconds) => SetFiniteDoubleProperty("sub-delay", seconds);

    public MpvStatus ResetSubtitleDelay() => SetSubtitleDelay(0);

    public MpvStatus SetAudioDelay(double seconds) => SetFiniteDoubleProperty("audio-delay", seconds);

    public MpvStatus ResetAudioDelay() => SetAudioDelay(0);

    private void StartPlaybackStateObservation()
    {
        ObserveProperty(DurationObserverId, "duration", MpvFormat.Double);
        ObserveProperty(PositionObserverId, "time-pos", MpvFormat.Double);
        ObserveProperty(PauseObserverId, "pause", MpvFormat.Flag);
        ObserveProperty(SpeedObserverId, "speed", MpvFormat.Double);
        ObserveProperty(VolumeObserverId, "volume", MpvFormat.Double);
        ObserveProperty(VideoWidthObserverId, "video-out-params/dw", MpvFormat.Long);
        ObserveProperty(VideoHeightObserverId, "video-out-params/dh", MpvFormat.Long);
        ObserveProperty(TrackListObserverId, "track-list", MpvFormat.Node);
        ObserveProperty(AudioTrackObserverId, "aid", MpvFormat.String);
        ObserveProperty(SubtitleTrackObserverId, "sid", MpvFormat.String);
        ObserveProperty(SecondarySubtitleTrackObserverId, "secondary-sid", MpvFormat.String);
        ObserveProperty(SubtitleDelayObserverId, "sub-delay", MpvFormat.Double);
        ObserveProperty(AudioDelayObserverId, "audio-delay", MpvFormat.Double);
        ObserveProperty(SubtitleVisibilityObserverId, "sub-visibility", MpvFormat.Flag);

        var eventLoopCts = new CancellationTokenSource();
        _eventLoopCts  = eventLoopCts;
        _eventLoopTask = Task.Run(() => ProcessEvents(eventLoopCts.Token));
    }

    private void ObserveProperty(ulong observerId, string name, MpvFormat format)
    {
        var status = LibMpv.ObserveProperty(_handle, observerId, name, format);
        if (status < 0)
        {
            HandleError(status, $"ObserveProperty {name}");
        }
    }

    private void ProcessEvents(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var eventPointer = LibMpv.WaitEvent(_handle, 0.05);
                if (eventPointer == nint.Zero)
                {
                    continue;
                }

                var mpvEvent = Marshal.PtrToStructure<MpvEvent>(eventPointer);
                switch (mpvEvent.eventId)
                {
                    case MpvEventId.None :
                        break;
                    case MpvEventId.PropertyChange :
                        ApplyObservedProperty(mpvEvent);
                        break;
                    case MpvEventId.StartFile :
                        ResetTracksForNewMedia();
                        PublishPlaybackEvent(mpvEvent);
                        break;
                    case MpvEventId.FileLoaded :
                    case MpvEventId.EndFile :
                    case MpvEventId.Seek :
                    case MpvEventId.PlaybackRestart :
                        PublishPlaybackEvent(mpvEvent);
                        break;
                    case MpvEventId.QueueOverflow :
                        _logger.LogWarning("mpv event queue overflowed; playback state may be temporarily stale.");
                        break;
                    case MpvEventId.Shutdown :
                        return;
                }
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "mpv event loop stopped unexpectedly.");
        }
    }

    private void ApplyObservedProperty(MpvEvent mpvEvent)
    {
        var property = mpvEvent.data == nint.Zero
            ? default
            : Marshal.PtrToStructure<MpvEventProperty>(mpvEvent.data);

        var currentState = Volatile.Read(ref _playbackState);
        var updatedState = mpvEvent.replyUserData switch
        {
            DurationObserverId => currentState with
            {
                Duration = ReadDouble(property) is { } duration ? Math.Max(0, duration) : 0
            },
            PositionObserverId => currentState with
            {
                Position = ReadDouble(property) is { } position ? Math.Max(0, position) : 0
            },
            PauseObserverId => currentState with { IsPaused = ReadFlag(property) ?? true },
            SpeedObserverId => currentState with { Speed = ReadDouble(property) is { } speed and > 0 ? speed : 1 },
            VolumeObserverId => currentState with
            {
                Volume = ReadDouble(property) is { } volume ? Math.Max(0, volume) : 100
            },
            VideoWidthObserverId => currentState with
            {
                VideoWidth = ReadLong(property) is { } width ? Math.Max(0, width) : 0
            },
            VideoHeightObserverId => currentState with
            {
                VideoHeight = ReadLong(property) is { } height ? Math.Max(0, height) : 0
            },
            TrackListObserverId => currentState with
            {
                Tracks = PrepareTracks(currentState.Tracks, MpvTrackListParser.Parse(property),
                                       currentState.AudioTrackId, currentState.SubtitleTrackId,
                                       currentState.SecondarySubtitleTrackId)
            },
            AudioTrackObserverId    => ApplyAudioTrackSelection(currentState, ReadTrackId(property)),
            SubtitleTrackObserverId => ApplySubtitleTrackSelection(currentState, ReadTrackId(property)),
            SecondarySubtitleTrackObserverId =>
                ApplySecondarySubtitleTrackSelection(currentState, ReadTrackId(property)),
            SubtitleDelayObserverId      => currentState with { SubtitleDelay = ReadFiniteDouble(property) },
            AudioDelayObserverId         => currentState with { AudioDelay = ReadFiniteDouble(property) },
            SubtitleVisibilityObserverId => currentState with { AreSubtitlesVisible = ReadFlag(property) ?? true },
            _                            => currentState
        };

        if (updatedState == currentState)
        {
            return;
        }

        Volatile.Write(ref _playbackState, updatedState);
        PlaybackStateChanged?.Invoke(
                                     this,
                                     new MpvPlaybackStateChangedEventArgs(currentState, updatedState));
    }

    private void ResetTracksForNewMedia()
    {
        var currentState = Volatile.Read(ref _playbackState);
        if (currentState.Tracks.IsEmpty                   &&
            currentState.AudioTrackId             == null &&
            currentState.SubtitleTrackId          == null &&
            currentState.SecondarySubtitleTrackId == null)
        {
            return;
        }

        var updatedState = currentState with
        {
            Tracks = [], AudioTrackId = null, SubtitleTrackId = null, SecondarySubtitleTrackId = null
        };

        Volatile.Write(ref _playbackState, updatedState);
        PlaybackStateChanged?.Invoke(this, new MpvPlaybackStateChangedEventArgs(currentState, updatedState));
    }

    private void PublishPlaybackEvent(MpvEvent mpvEvent)
    {
        MpvEndFileReason? endReason    = null;
        MpvStatus?        error        = null;
        string?           errorMessage = null;

        if (mpvEvent.eventId == MpvEventId.EndFile && mpvEvent.data != nint.Zero)
        {
            var endFile = Marshal.PtrToStructure<MpvEventEndFile>(mpvEvent.data);
            if (Enum.IsDefined((MpvEndFileReason)endFile.reason))
            {
                endReason = (MpvEndFileReason)endFile.reason;
            }

            if (endFile.error < 0)
            {
                error        = (MpvStatus)endFile.error;
                errorMessage = LibMpv.GetError(error.Value);
            }
        }

        PlaybackEvent?.Invoke(this,
                              new MpvPlaybackEventArgs(mpvEvent.eventId, PlaybackState, endReason, error,
                                                       errorMessage));
    }

    private static double? ReadDouble(MpvEventProperty property) =>
        property.format == MpvFormat.Double && property.data != nint.Zero
            ? BitConverter.Int64BitsToDouble(Marshal.ReadInt64(property.data))
            : null;


    private static bool? ReadFlag(MpvEventProperty property) =>
        property.format == MpvFormat.Flag && property.data != nint.Zero ? Marshal.ReadInt32(property.data) != 0 : null;


    private static long? ReadLong(MpvEventProperty property) =>
        property.format == MpvFormat.Long && property.data != nint.Zero ? Marshal.ReadInt64(property.data) : null;


    private static string? ReadString(MpvEventProperty property)
    {
        if (property.format != MpvFormat.String || property.data == nint.Zero)
        {
            return null;
        }

        var valuePointer = Marshal.ReadIntPtr(property.data);
        return valuePointer == nint.Zero ? null : Marshal.PtrToStringUTF8(valuePointer);
    }

    private static long? ReadTrackId(MpvEventProperty property)
    {
        var value = ReadString(property);
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var trackId) && trackId > 0
            ? trackId
            : null;
    }

    private static double ReadFiniteDouble(MpvEventProperty property) =>
        ReadDouble(property) is { } value && double.IsFinite(value) ? value : 0;


    private static MpvPlaybackState ApplyAudioTrackSelection(MpvPlaybackState state, long? trackId)
    {
        return state with
        {
            Tracks = PrepareTracks(state.Tracks, state.Tracks, trackId, state.SubtitleTrackId,
                                   state.SecondarySubtitleTrackId),
            AudioTrackId = trackId
        };
    }

    private static MpvPlaybackState ApplySubtitleTrackSelection(MpvPlaybackState state, long? trackId)
    {
        return state with
        {
            Tracks = PrepareTracks(state.Tracks, state.Tracks, state.AudioTrackId, trackId,
                                   state.SecondarySubtitleTrackId),
            SubtitleTrackId = trackId
        };
    }

    private static MpvPlaybackState ApplySecondarySubtitleTrackSelection(MpvPlaybackState state, long? trackId)
    {
        return state with
        {
            Tracks = PrepareTracks(state.Tracks, state.Tracks, state.AudioTrackId, state.SubtitleTrackId, trackId),
            SecondarySubtitleTrackId = trackId
        };
    }

    private static ImmutableArray<MediaTrackInfo> PrepareTracks(ImmutableArray<MediaTrackInfo> currentTracks,
                                                                ImmutableArray<MediaTrackInfo> candidateTracks,
                                                                long? audioTrackId, long? subtitleTrackId,
                                                                long? secondarySubtitleTrackId)
    {
        if (candidateTracks.IsEmpty)
        {
            return currentTracks.IsEmpty ? currentTracks : [];
        }

        var builder = ImmutableArray.CreateBuilder<MediaTrackInfo>(candidateTracks.Length);
        foreach (var track in candidateTracks)
        {
            var isSelected = track.Type switch
            {
                MediaTrackType.Audio    => track.Id == audioTrackId,
                MediaTrackType.Subtitle => track.Id == subtitleTrackId || track.Id == secondarySubtitleTrackId,
                _                       => track.IsSelected
            };
            builder.Add(track.IsSelected == isSelected ? track : track with { IsSelected = isSelected });
        }

        var normalizedTracks = builder.MoveToImmutable();
        return currentTracks.AsSpan().SequenceEqual(normalizedTracks.AsSpan()) ? currentTracks : normalizedTracks;
    }

    private MpvStatus SetTrackProperty(string propertyName, long trackId) => trackId > 0
        ? SetProperty(propertyName, trackId.ToString(CultureInfo.InvariantCulture))
        : MpvStatus.InvalidParameter;


    private MpvStatus SetFiniteDoubleProperty(string propertyName, double value) => double.IsFinite(value)
        ? SetProperty(propertyName, value)
        : MpvStatus.InvalidParameter;


    private void HandleError(MpvStatus err, string msg)
    {
        if (err >= 0) return;
        _logger.LogError("{Msg}: {Error}", msg, LibMpv.GetError(err));
    }

    private void CheckDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MpvService));
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        _eventLoopCts?.Cancel();
        try
        {
            _eventLoopTask?.Wait(TimeSpan.FromMilliseconds(250));
        }
        catch (AggregateException) when (_eventLoopCts?.IsCancellationRequested == true)
        {
            // Expected while shutting down the event loop.
        }

        _eventLoopCts?.Dispose();
        _eventLoopCts  = null;
        _eventLoopTask = null;

        if (_renderContext != IntPtr.Zero)
        {
            LibMpv.RenderContextFree(_renderContext);
            _renderContext = IntPtr.Zero;
        }

        if (_handle != IntPtr.Zero)
        {
            LibMpv.Destroy(_handle);
            _handle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}
