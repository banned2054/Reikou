using Reikou.Models;
using Reikou.Models.Enums;
using System.Collections.Immutable;

namespace Reikou.Services;

public sealed record MpvPlaybackState(
    double                         Duration,
    double                         Position,
    bool                           IsPaused,
    double                         Speed,
    double                         Volume,
    long                           VideoWidth,
    long                           VideoHeight,
    ImmutableArray<MediaTrackInfo> Tracks,
    long?                          AudioTrackId,
    long?                          SubtitleTrackId,
    long?                          SecondarySubtitleTrackId,
    double                         SubtitleDelay,
    double                         AudioDelay,
    bool                           AreSubtitlesVisible,
    string?                        HardwareDecoder)
{
    public static MpvPlaybackState Default { get; } = new(0,
                                                          0,
                                                          true,
                                                          1,
                                                          100,
                                                          0,
                                                          0,
                                                          [],
                                                          null,
                                                          null,
                                                          null,
                                                          0,
                                                          0,
                                                          true,
                                                          null);

    public long   MediaRevision   { get; init; }
    public long?  PlaylistEntryId { get; init; }
    public bool   IsBuffering     { get; init; }
    public bool   IsSeekable      { get; init; }
    public bool   IsEof           { get; init; }
    public double CacheDuration   { get; init; }

    public double VideoAspectRatio => VideoWidth > 0 && VideoHeight > 0 ? (double)VideoWidth / VideoHeight : 0;

    public MediaTrackInfo? CurrentAudioTrack => FindTrack(AudioTrackId, MediaTrackType.Audio);

    public MediaTrackInfo? CurrentSubtitleTrack => FindTrack(SubtitleTrackId, MediaTrackType.Subtitle);

    public MediaTrackInfo? CurrentSecondarySubtitleTrack =>
        FindTrack(SecondarySubtitleTrackId, MediaTrackType.Subtitle);

    private MediaTrackInfo? FindTrack(long? trackId, MediaTrackType type)
    {
        if (trackId == null) return null;

        foreach (var track in Tracks)
            if (track.Id == trackId && track.Type == type)
                return track;

        return null;
    }
}
