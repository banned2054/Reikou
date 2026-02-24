using System;

namespace TestMpv.Enums;

[Flags]
public enum MpvRenderUpdateFlag
{
    None   = 0,
    Frame  = 1,
    Redraw = 2
}
