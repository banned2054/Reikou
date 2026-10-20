using Reikou.Models.Enums;
using System;

namespace Reikou.Services;

public sealed class MpvPlaybackStateChangedEventArgs(MpvPlaybackState previousState, MpvPlaybackState currentState)
    : EventArgs
{
    public MpvPlaybackState PreviousState { get; } = previousState;
    public MpvPlaybackState CurrentState  { get; } = currentState;
}

public sealed class MpvPlaybackEventArgs(
    MpvEventId        eventId,
    MpvPlaybackState  state,
    MpvEndFileReason? endReason       = null,
    MpvStatus?        error           = null,
    string?           errorMessage    = null,
    long              mediaRevision   = 0,
    long?             playlistEntryId = null) : EventArgs
{
    public MpvEventId        EventId         { get; } = eventId;
    public MpvPlaybackState  State           { get; } = state;
    public MpvEndFileReason? EndReason       { get; } = endReason;
    public MpvStatus?        Error           { get; } = error;
    public string?           ErrorMessage    { get; } = errorMessage;
    public long              MediaRevision   { get; } = mediaRevision;
    public long?             PlaylistEntryId { get; } = playlistEntryId;
}

internal sealed class SessionMediaEventArgs(
    string            path,
    long              mediaRevision,
    MpvEndFileReason? endReason    = null,
    string?           errorMessage = null) : EventArgs
{
    public string            Path          { get; } = path;
    public long              MediaRevision { get; } = mediaRevision;
    public MpvEndFileReason? EndReason     { get; } = endReason;
    public string?           ErrorMessage  { get; } = errorMessage;
}
