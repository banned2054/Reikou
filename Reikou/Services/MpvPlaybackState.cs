namespace Reikou.Services;

public sealed record MpvPlaybackState(
    double Duration,
    double Position,
    bool IsPaused,
    double Speed,
    double Volume,
    long VideoWidth,
    long VideoHeight)
{
    public static MpvPlaybackState Default { get; } = new(
        Duration    : 0,
        Position    : 0,
        IsPaused    : true,
        Speed       : 1,
        Volume      : 100,
        VideoWidth  : 0,
        VideoHeight : 0
    );

    public double VideoAspectRatio => VideoWidth > 0 && VideoHeight > 0
        ? (double)VideoWidth / VideoHeight
        : 0;
}
