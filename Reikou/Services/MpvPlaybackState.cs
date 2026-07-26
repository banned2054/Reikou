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
    public static MpvPlaybackState Default { get; } = new(
                                                          Duration : 0,
                                                          Position : 0,
                                                          IsPaused : true,
                                                          Speed : 1,
                                                          Volume : 100,
                                                          VideoWidth : 0,
                                                          VideoHeight : 0,
                                                          Tracks : [],
                                                          AudioTrackId : null,
                                                          SubtitleTrackId : null,
                                                          SecondarySubtitleTrackId : null,
                                                          SubtitleDelay : 0,
                                                          AudioDelay : 0,
                                                          AreSubtitlesVisible : true,
                                                          HardwareDecoder : null
                                                         );

    public double VideoAspectRatio => VideoWidth > 0 && VideoHeight > 0 ? (double)VideoWidth / VideoHeight : 0;

    public MediaTrackInfo? CurrentAudioTrack => FindTrack(AudioTrackId, MediaTrackType.Audio);

    public MediaTrackInfo? CurrentSubtitleTrack => FindTrack(SubtitleTrackId, MediaTrackType.Subtitle);

    public MediaTrackInfo? CurrentSecondarySubtitleTrack =>
        FindTrack(SecondarySubtitleTrackId, MediaTrackType.Subtitle);

    private MediaTrackInfo? FindTrack(long? trackId, MediaTrackType type)
    {
        if (trackId == null)
        {
            return null;
        }

        foreach (var track in Tracks)
        {
            if (track.Id == trackId && track.Type == type)
            {
                return track;
            }
        }

        return null;
    }
}
