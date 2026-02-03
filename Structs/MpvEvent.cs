using System.Runtime.InteropServices;
using TestMpv.Enums;

namespace TestMpv.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvEvent
{
    public MpvEventId eventId;
    public int        error;
    public ulong      replyUserData;
    public nint       data;
}
