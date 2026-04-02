namespace TestMpv.Models.Enums;

public enum MpvRenderParamType
{
    Invalid            = 0,
    ApiType            = 1,
    OpenglInitParams   = 2,
    OpenglFbo          = 3,
    FlipY              = 4,
    Depth              = 5,
    IccProfile         = 6,
    AmbientLight       = 7,
    X11Display         = 8,
    WlDisplay          = 9,
    AdvancedControl    = 10,
    NextFrameInfo      = 11,
    BlockForTargetTime = 12,
    SkipRendering      = 13,
    DrmDisplay         = 14,
    DrmDrawSurfaceSize = 15,
    DrmDisplayV2       = 16,
}
