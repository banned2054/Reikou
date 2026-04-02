using System;

namespace TestMpv.Models.Enums;

[Flags]
public enum MpvRenderUpdateFlag
{
    None   = 0,
    Frame  = 1,
    Redraw = 2
}
