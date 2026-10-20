using System;

namespace Reikou.Services;

public enum PlaybackStatusKind
{
    Idle,
    Opening,
    Playing,
    Paused,
    Stopped,
    Ended,
    Failed
}

public sealed record PlaybackStatus(
    string?            Path          = null,
    long               MediaRevision = 0,
    PlaybackStatusKind State         = PlaybackStatusKind.Idle,
    bool               IsBuffering   = false,
    bool               IsSeekable    = false,
    string?            ErrorMessage  = null);

public sealed class PlaybackStatusChangedEventArgs(PlaybackStatus status) : EventArgs
{
    public PlaybackStatus Status { get; } = status;
}
