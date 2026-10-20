using Banned.Avalonia.Danmaku.Models.Playback;
using Banned.Avalonia.Danmaku.Models.Playback.Events;
using Reikou.Models.Enums;
using Reikou.Models.Settings;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Reikou.Services;

internal sealed class PlaybackCoordinator : IDisposable
{
    private readonly Action<Action>         _dispatch;
    private readonly Lock                   _gate = new();
    private readonly Func<PlaybackSettings> _getPolicy;
    private readonly PlaybackHistoryStore   _history;

    private readonly Func<string, CancellationToken, Task> _loadNative;

    private readonly Action<Exception>?    _reportError;
    private readonly Action<double>        _seek;
    private readonly MpvPlaybackSession    _session;
    private readonly Action                _stop;
    private readonly Func<string, string?> _tryAdvanceNaturally;

    private bool  _closed;
    private Task? _closeTask;
    private bool  _disposed;
    private long  _generation;
    private long  _naturallyEndedRevision = -1;

    private CancellationTokenSource? _requestCts;
    private long                     _resumedRevision = -1;
    private PlaybackStatus           _status          = new();

    public PlaybackCoordinator(MpvPlaybackSession     session, PlaybackHistoryStore history, Action<Action> dispatcher,
                               Func<PlaybackSettings> getPolicy, Func<string, CancellationToken, Task> loadNative,
                               Action<double>         seek, Action stop, Func<string, string?> tryAdvanceNaturally,
                               Action<Exception>?     reportError = null)
    {
        _session                    =  session;
        _history                    =  history;
        _dispatch                   =  dispatcher;
        _getPolicy                  =  getPolicy;
        _loadNative                 =  loadNative;
        _seek                       =  seek;
        _stop                       =  stop;
        _tryAdvanceNaturally        =  tryAdvanceNaturally;
        _reportError                =  reportError;
        _session.StateChanged       += OnStateChanged;
        _session.NativeStateChanged += OnNativeStateChanged;
        _session.Loaded             += OnLoaded;
        _session.Ended              += OnEnded;
        _session.PlaybackFailed     += OnFailed;
    }

    public PlaybackStatus Status
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            ++_generation;
            _requestCts?.Cancel();
            Unsubscribe();
        }
    }

    public event EventHandler<PlaybackStatusChangedEventArgs>? StatusChanged;

    public async Task OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        path = Path.GetFullPath(path);
        long                    generation;
        CancellationTokenSource request;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed || _closed, this);
            generation = ++_generation;
            _requestCts?.Cancel();
            request     = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _requestCts = request;
            CaptureProgress();
        }

        try
        {
            await SaveHistoryAsync().ConfigureAwait(false);
            request.Token.ThrowIfCancellationRequested();
            Task loadTask;
            lock (_gate)
            {
                if (!IsCurrent(generation)) return;
                _session.OpenMedia(path);
                if (!_getPolicy().RememberPosition) _history.ClearAllProgress();
                _history.RecordOpened(path);
                PublishStatus(new PlaybackStatus(path, _session.MediaRevision, PlaybackStatusKind.Opening));
                loadTask = _loadNative(path, request.Token);
            }

            await loadTask.ConfigureAwait(false);
            request.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            lock (_gate)
            {
                if (IsCurrent(generation)) _session.FailCurrentMedia(exception.Message);
            }
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_requestCts, request)) _requestCts = null;
            }

            request.Dispose();
        }
    }

    public async Task StopAsync(bool clearMedia = false, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_closed || _disposed) return;
            ++_generation;
            _requestCts?.Cancel();
            CaptureProgress();
            // Mark stopped before native stop can produce EOF/END_FILE.
            _session.StopCurrentMedia();
            _stop();
            if (clearMedia) _session.ClearMedia();
            PublishStatus(new PlaybackStatus(_session.CurrentPath, _session.MediaRevision,
                                             clearMedia ? PlaybackStatusKind.Idle : PlaybackStatusKind.Stopped));
        }

        await SaveHistoryAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task CloseAsync()
    {
        lock (_gate)
        {
            if (_closeTask != null) return _closeTask;
            if (_disposed) return Task.CompletedTask;
            _closed = true;
            ++_generation;
            _requestCts?.Cancel();
            CaptureProgress();
            _session.StopCurrentMedia();
            _stop();
            Unsubscribe();
            // No dispatcher dependency: Closing must persist before render context teardown.
            return _closeTask = SaveHistoryAsync();
        }
    }

    private void CaptureProgress()
    {
        if (!_getPolicy().RememberPosition)
        {
            _history.ClearAllProgress();
            return;
        }

        var path = _session.CurrentPath;
        if (path == null || _session.State is PlaybackState.Opening
                                           or PlaybackState.Failed
                                           or PlaybackState.Idle) return;
        _history.RecordProgress(path, _session.Position.TotalSeconds, _session.Duration?.TotalSeconds ?? 0,
                                true, _naturallyEndedRevision == _session.MediaRevision);
    }

    private async Task SaveHistoryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _history.SaveAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _reportError?.Invoke(e);
            lock (_gate)
            {
                if (!_closed && !_disposed)
                    PublishStatus(_status with { ErrorMessage = $"Unable to save playback history: {e.Message}" });
            }
        }
    }

    private void OnLoaded(object? sender, SessionMediaEventArgs e)
    {
        long generation;
        lock (_gate)
        {
            generation = _generation;
        }

        _dispatch(() =>
        {
            lock (_gate)
            {
                if (!IsCurrent(generation, e.MediaRevision) || _resumedRevision  == e.MediaRevision) return;
                if (!_session.NativeState.IsSeekable        || _session.Duration == null) return;
                _resumedRevision = e.MediaRevision;
                if (_getPolicy().RememberPosition                      &&
                    _history.GetResumePosition(e.Path) is { } position &&
                    PlaybackHistoryStore.IsResumePosition(position, _session.Duration?.TotalSeconds ?? 0))
                    _seek(position);
                UpdateStatus();
            }
        });
    }

    private void OnEnded(object? sender, SessionMediaEventArgs e)
    {
        if (e.EndReason != MpvEndFileReason.Eof) return;
        long generation;
        lock (_gate)
        {
            generation = _generation;
            if (!IsCurrent(generation, e.MediaRevision) || _getPolicy().LoopFile) return;
            _naturallyEndedRevision = e.MediaRevision;
            _history.ClearProgress(e.Path);
        }

        _dispatch(() =>
        {
            lock (_gate)
            {
                if (!IsCurrent(generation, e.MediaRevision) || _getPolicy().LoopFile) return;
                var next = _tryAdvanceNaturally(e.Path);
                _ = next != null ? OpenAsync(next) : SaveHistoryAsync();
            }
        });
    }

    private void OnFailed(object? sender, PlaybackFailedEventArgs e)
    {
        lock (_gate)
        {
            PublishStatus(new PlaybackStatus(_session.CurrentPath, _session.MediaRevision,
                                             PlaybackStatusKind.Failed, ErrorMessage : e.Message));
        }
    }

    private void OnStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        lock (_gate)
        {
            if (e.CurrentState is PlaybackState.Playing
                               or PlaybackState.Paused)
                _naturallyEndedRevision = -1;
            UpdateStatus();
        }
    }

    private void OnNativeStateChanged(object? sender, EventArgs e)
    {
        SessionMediaEventArgs? loaded = null;
        lock (_gate)
        {
            UpdateStatus();
            if (_session.State is
                    PlaybackState.Playing or PlaybackState.Paused &&
                _session.CurrentPath is { } path                  &&
                _resumedRevision != _session.MediaRevision        &&
                _session.NativeState.IsSeekable                   &&
                _session.Duration != null)
                loaded = new SessionMediaEventArgs(path, _session.MediaRevision);
        }

        if (loaded != null) OnLoaded(sender, loaded);
    }

    private void UpdateStatus()
    {
        if (_closed || _disposed) return;
        var state = _session.State switch
        {
            PlaybackState.Opening => PlaybackStatusKind.Opening,
            PlaybackState.Playing => PlaybackStatusKind.Playing,
            PlaybackState.Paused  => PlaybackStatusKind.Paused,
            PlaybackState.Ended   => PlaybackStatusKind.Ended,
            PlaybackState.Stopped => PlaybackStatusKind.Stopped,
            PlaybackState.Failed  => PlaybackStatusKind.Failed,
            _                     => PlaybackStatusKind.Idle
        };
        var native = _session.NativeState;
        PublishStatus(new PlaybackStatus(_session.CurrentPath, _session.MediaRevision, state,
                                         native.IsBuffering, native.IsSeekable,
                                         state == PlaybackStatusKind.Failed ? _status.ErrorMessage : null));
    }

    private void PublishStatus(PlaybackStatus status)
    {
        if (_closed || _disposed || status == _status) return;
        _status = status;
        var generation = _generation;
        _dispatch(() =>
        {
            lock (_gate)
            {
                if (!IsCurrent(generation, status.MediaRevision) || !ReferenceEquals(_status, status)) return;
                StatusChanged?.Invoke(this, new PlaybackStatusChangedEventArgs(status));
            }
        });
    }

    private bool IsCurrent(long generation, long? revision = null) => !_closed && !_disposed &&
                                                                      generation == _generation &&
                                                                      (revision == null ||
                                                                       revision == _session.MediaRevision);

    private void Unsubscribe()
    {
        _session.StateChanged       -= OnStateChanged;
        _session.NativeStateChanged -= OnNativeStateChanged;
        _session.Loaded             -= OnLoaded;
        _session.Ended              -= OnEnded;
        _session.PlaybackFailed     -= OnFailed;
    }
}
