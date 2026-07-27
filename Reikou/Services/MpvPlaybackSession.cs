using Banned.Avalonia.Danmaku.Models.Playback;
using Banned.Avalonia.Danmaku.Models.Playback.Events;
using Banned.Avalonia.Danmaku.Services.Playback;
using Reikou.Models.Enums;
using System;
using System.IO;
using System.Threading;
using DanmakuPlaybackState = Banned.Avalonia.Danmaku.Models.Playback.PlaybackState;

namespace Reikou.Services;

internal sealed class MpvPlaybackSession : IPlaybackSession, IDisposable
{
    private readonly Lock       _gate = new();
    private readonly MpvService _service;

    private DanmakuPlaybackState _state = DanmakuPlaybackState.Idle;
    private PlaybackMediaId?     _currentMediaId;

    private TimeSpan? _duration;
    private TimeSpan  _position;
    private TimeSpan  _requestedSeekPosition;

    private double _playbackRate;
    private long   _mediaRevision;
    private bool   _isSeeking;
    private bool   _awaitingStartFile;
    private bool   _hasStartedCurrentMedia;
    private bool   _disposed;

    public MpvPlaybackSession(MpvService service)
    {
        _service      = service;
        _playbackRate = SanitizeRate(service.PlaybackState.Speed);

        _service.PlaybackStateChanged += OnPlaybackStateChanged;
        _service.PlaybackEvent        += OnPlaybackEvent;
    }

    public event EventHandler<MediaChangedEventArgs>?         MediaChanged;
    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;
    public event EventHandler<PositionChangedEventArgs>?      PositionChanged;
    public event EventHandler<SeekStartedEventArgs>?          SeekStarted;
    public event EventHandler<SeekCompletedEventArgs>?        SeekCompleted;
    public event EventHandler<PlaybackRateChangedEventArgs>?  PlaybackRateChanged;
    public event EventHandler<DurationChangedEventArgs>?      DurationChanged;
    public event EventHandler<PlaybackFailedEventArgs>?       PlaybackFailed;

    public PlaybackMediaId? CurrentMediaId
    {
        get
        {
            lock (_gate)
            {
                return _currentMediaId;
            }
        }
    }

    public long MediaRevision
    {
        get
        {
            lock (_gate)
            {
                return _mediaRevision;
            }
        }
    }

    public DanmakuPlaybackState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    public TimeSpan Position
    {
        get
        {
            lock (_gate)
            {
                return _position;
            }
        }
    }

    public TimeSpan? Duration
    {
        get
        {
            lock (_gate)
            {
                return _duration;
            }
        }
    }

    public double PlaybackRate
    {
        get
        {
            lock (_gate)
            {
                return _playbackRate;
            }
        }
    }

    public bool IsSeeking
    {
        get
        {
            lock (_gate)
            {
                return _isSeeking;
            }
        }
    }

    public void OpenMedia(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        MediaChangedEventArgs          mediaChanged;
        PlaybackStateChangedEventArgs? stateChanged;
        PositionChangedEventArgs?      positionChanged;
        DurationChangedEventArgs?      durationChanged;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var previousMedia    = _currentMediaId;
            var previousState    = _state;
            var previousPosition = _position;
            var previousDuration = _duration;

            _currentMediaId = new PlaybackMediaId(Path.GetFullPath(filePath));
            _mediaRevision++;
            _state                  = DanmakuPlaybackState.Opening;
            _position               = TimeSpan.Zero;
            _duration               = null;
            _isSeeking              = false;
            _requestedSeekPosition  = TimeSpan.Zero;
            _awaitingStartFile      = true;
            _hasStartedCurrentMedia = false;

            mediaChanged = new MediaChangedEventArgs(previousMedia, _currentMediaId, _mediaRevision);
            stateChanged = previousState == _state
                ? null
                : new PlaybackStateChangedEventArgs(previousState, _state, _position, _mediaRevision);
            positionChanged = previousPosition == _position
                ? null
                : new PositionChangedEventArgs(previousPosition, _position, _mediaRevision);
            durationChanged = previousDuration == _duration
                ? null
                : new DurationChangedEventArgs(previousDuration, _duration, _mediaRevision);
        }

        MediaChanged?.Invoke(this, mediaChanged);
        if (durationChanged != null)
        {
            DurationChanged?.Invoke(this, durationChanged);
        }

        if (positionChanged != null)
        {
            PositionChanged?.Invoke(this, positionChanged);
        }

        if (stateChanged != null)
        {
            StateChanged?.Invoke(this, stateChanged);
        }
    }

    public void ClearMedia()
    {
        MediaChangedEventArgs          mediaChanged;
        PlaybackStateChangedEventArgs? stateChanged;
        PositionChangedEventArgs?      positionChanged;
        DurationChangedEventArgs?      durationChanged;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var previousMedia    = _currentMediaId;
            var previousState    = _state;
            var previousPosition = _position;
            var previousDuration = _duration;

            _currentMediaId = null;
            _mediaRevision++;
            _state                  = DanmakuPlaybackState.Idle;
            _position               = TimeSpan.Zero;
            _duration               = null;
            _isSeeking              = false;
            _requestedSeekPosition  = TimeSpan.Zero;
            _awaitingStartFile      = false;
            _hasStartedCurrentMedia = false;

            mediaChanged = new MediaChangedEventArgs(previousMedia, null, _mediaRevision);
            stateChanged = previousState == _state
                ? null
                : new PlaybackStateChangedEventArgs(previousState, _state, _position, _mediaRevision);
            positionChanged = previousPosition == _position
                ? null
                : new PositionChangedEventArgs(previousPosition, _position, _mediaRevision);
            durationChanged = previousDuration == _duration
                ? null
                : new DurationChangedEventArgs(previousDuration, _duration, _mediaRevision);
        }

        MediaChanged?.Invoke(this, mediaChanged);
        if (durationChanged != null)
        {
            DurationChanged?.Invoke(this, durationChanged);
        }

        if (positionChanged != null)
        {
            PositionChanged?.Invoke(this, positionChanged);
        }

        if (stateChanged != null)
        {
            StateChanged?.Invoke(this, stateChanged);
        }
    }

    public void BeginSeek(TimeSpan requestedPosition)
    {
        SeekStartedEventArgs? seekStarted = null;

        lock (_gate)
        {
            if (_disposed || _currentMediaId == null)
            {
                return;
            }

            _requestedSeekPosition = ClampPosition(requestedPosition, _duration);
            if (!_isSeeking)
            {
                _isSeeking  = true;
                seekStarted = new SeekStartedEventArgs(_position, _requestedSeekPosition, _mediaRevision);
            }
        }

        if (seekStarted != null)
        {
            SeekStarted?.Invoke(this, seekStarted);
        }
    }

    public void CancelSeek()
    {
        SeekCompletedEventArgs? seekCompleted;

        lock (_gate)
        {
            if (_disposed || !_isSeeking)
            {
                return;
            }

            _isSeeking    = false;
            seekCompleted = new SeekCompletedEventArgs(_requestedSeekPosition, _position, _mediaRevision);
        }

        SeekCompleted?.Invoke(this, seekCompleted);
    }

    public void FailCurrentMedia(string message)
    {
        PlaybackStateChangedEventArgs? stateChanged;
        PlaybackFailedEventArgs?       playbackFailed;

        lock (_gate)
        {
            if (_disposed || _currentMediaId == null)
            {
                return;
            }

            var previousState = _state;
            _state     = DanmakuPlaybackState.Failed;
            _isSeeking = false;

            stateChanged = previousState == _state
                ? null
                : new PlaybackStateChangedEventArgs(previousState, _state, _position, _mediaRevision);
            playbackFailed = new PlaybackFailedEventArgs(_currentMediaId, _mediaRevision, message);
        }

        if (stateChanged != null)
        {
            StateChanged?.Invoke(this, stateChanged);
        }

        PlaybackFailed?.Invoke(this, playbackFailed);
    }

    private void OnPlaybackStateChanged(object? sender, MpvPlaybackStateChangedEventArgs e)
    {
        PositionChangedEventArgs?      positionChanged = null;
        DurationChangedEventArgs?      durationChanged = null;
        PlaybackRateChangedEventArgs?  rateChanged     = null;
        PlaybackStateChangedEventArgs? stateChanged    = null;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var currentRate = SanitizeRate(e.CurrentState.Speed);
            if (Math.Abs(currentRate - _playbackRate) > 0.0001)
            {
                var previousRate = _playbackRate;
                _playbackRate = currentRate;
                if (_currentMediaId != null)
                {
                    rateChanged =
                        new PlaybackRateChangedEventArgs(previousRate, _playbackRate, _position, _mediaRevision);
                }
            }

            if (_currentMediaId == null || !_hasStartedCurrentMedia)
            {
                return;
            }

            var currentDuration = ToDuration(e.CurrentState.Duration);
            if (_duration != currentDuration)
            {
                var previousDuration = _duration;
                _duration       = currentDuration;
                durationChanged = new DurationChangedEventArgs(previousDuration, _duration, _mediaRevision);
            }

            var currentPosition = ToPosition(e.CurrentState.Position);
            if (_position != currentPosition)
            {
                var previousPosition = _position;
                _position       = currentPosition;
                positionChanged = new PositionChangedEventArgs(previousPosition, _position, _mediaRevision);
            }

            if (_state is DanmakuPlaybackState.Ready or DanmakuPlaybackState.Playing or DanmakuPlaybackState.Paused)
            {
                var currentState = e.CurrentState.IsPaused ? DanmakuPlaybackState.Paused : DanmakuPlaybackState.Playing;
                if (_state != currentState)
                {
                    var previousState = _state;
                    _state       = currentState;
                    stateChanged = new PlaybackStateChangedEventArgs(previousState, _state, _position, _mediaRevision);
                }
            }
        }

        if (durationChanged != null)
        {
            DurationChanged?.Invoke(this, durationChanged);
        }

        if (positionChanged != null)
        {
            PositionChanged?.Invoke(this, positionChanged);
        }

        if (rateChanged != null)
        {
            PlaybackRateChanged?.Invoke(this, rateChanged);
        }

        if (stateChanged != null)
        {
            StateChanged?.Invoke(this, stateChanged);
        }
    }

    private void OnPlaybackEvent(object? sender, MpvPlaybackEventArgs e)
    {
        switch (e.EventId)
        {
            case MpvEventId.StartFile :
                HandleStartFile();
                break;
            case MpvEventId.FileLoaded :
                HandleFileLoaded(e.State);
                break;
            case MpvEventId.Seek :
                HandleSeekStarted(e.State);
                break;
            case MpvEventId.PlaybackRestart :
                HandleSeekCompleted(e.State);
                break;
            case MpvEventId.EndFile :
                HandleEndFile(e);
                break;
        }
    }

    private void HandleStartFile()
    {
        PlaybackStateChangedEventArgs? stateChanged = null;

        lock (_gate)
        {
            if (_disposed || _currentMediaId == null)
            {
                return;
            }

            _awaitingStartFile      = false;
            _hasStartedCurrentMedia = true;
            if (_state != DanmakuPlaybackState.Opening)
            {
                var previousState = _state;
                _state       = DanmakuPlaybackState.Opening;
                stateChanged = new PlaybackStateChangedEventArgs(previousState, _state, _position, _mediaRevision);
            }
        }

        if (stateChanged != null)
        {
            StateChanged?.Invoke(this, stateChanged);
        }
    }

    private void HandleFileLoaded(MpvPlaybackState serviceState)
    {
        PlaybackStateChangedEventArgs? stateChanged;

        lock (_gate)
        {
            if (_disposed || _currentMediaId == null || _awaitingStartFile || !_hasStartedCurrentMedia)
            {
                return;
            }

            var previousState = _state;
            _state = serviceState.IsPaused ? DanmakuPlaybackState.Paused : DanmakuPlaybackState.Playing;
            stateChanged = previousState == _state
                ? null
                : new PlaybackStateChangedEventArgs(previousState, _state, _position, _mediaRevision);
        }

        if (stateChanged != null)
        {
            StateChanged?.Invoke(this, stateChanged);
        }
    }

    private void HandleSeekStarted(MpvPlaybackState serviceState)
    {
        SeekStartedEventArgs? seekStarted = null;

        lock (_gate)
        {
            if (_disposed || _currentMediaId == null || !_hasStartedCurrentMedia || _isSeeking)
            {
                return;
            }

            _position              = ToPosition(serviceState.Position);
            _requestedSeekPosition = _position;
            _isSeeking             = true;
            seekStarted            = new SeekStartedEventArgs(_position, _requestedSeekPosition, _mediaRevision);
        }

        SeekStarted?.Invoke(this, seekStarted);
    }

    private void HandleSeekCompleted(MpvPlaybackState serviceState)
    {
        SeekCompletedEventArgs? seekCompleted = null;

        lock (_gate)
        {
            if (_disposed || !_isSeeking)
            {
                return;
            }

            _position     = ToPosition(serviceState.Position);
            _isSeeking    = false;
            seekCompleted = new SeekCompletedEventArgs(_requestedSeekPosition, _position, _mediaRevision);
        }

        SeekCompleted?.Invoke(this, seekCompleted);
    }

    private void HandleEndFile(MpvPlaybackEventArgs e)
    {
        PlaybackStateChangedEventArgs? stateChanged   = null;
        PlaybackFailedEventArgs?       playbackFailed = null;

        lock (_gate)
        {
            if (_disposed || _currentMediaId == null || _awaitingStartFile || !_hasStartedCurrentMedia)
            {
                return;
            }

            var previousState = _state;
            _isSeeking = false;

            if (e.EndReason == MpvEndFileReason.Redirect)
            {
                _state                  = DanmakuPlaybackState.Opening;
                _awaitingStartFile      = true;
                _hasStartedCurrentMedia = false;
            }
            else if (e.EndReason == MpvEndFileReason.Eof)
            {
                _state = DanmakuPlaybackState.Ended;
            }
            else if (e.EndReason == MpvEndFileReason.Error)
            {
                _state = DanmakuPlaybackState.Failed;
                playbackFailed = new PlaybackFailedEventArgs(_currentMediaId, _mediaRevision,
                                                             e.ErrorMessage ?? "mpv failed to play the current media.");
            }
            else
            {
                _state = DanmakuPlaybackState.Stopped;
            }

            if (previousState != _state)
            {
                stateChanged = new PlaybackStateChangedEventArgs(previousState, _state, _position, _mediaRevision);
            }
        }

        if (stateChanged != null)
        {
            StateChanged?.Invoke(this, stateChanged);
        }

        if (playbackFailed != null)
        {
            PlaybackFailed?.Invoke(this, playbackFailed);
        }
    }

    private static TimeSpan ToPosition(double seconds) =>
        TimeSpan.FromSeconds(double.IsFinite(seconds) ? Math.Max(0, seconds) : 0);


    private static TimeSpan? ToDuration(double seconds) =>
        double.IsFinite(seconds) && seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;


    private static double SanitizeRate(double rate) => double.IsFinite(rate) && rate > 0 ? rate : 1;


    private static TimeSpan ClampPosition(TimeSpan position, TimeSpan? duration)
    {
        var maximum = duration ?? TimeSpan.MaxValue;
        return position < TimeSpan.Zero ? TimeSpan.Zero : position > maximum ? maximum : position;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _service.PlaybackStateChanged -= OnPlaybackStateChanged;
        _service.PlaybackEvent        -= OnPlaybackEvent;
    }
}
