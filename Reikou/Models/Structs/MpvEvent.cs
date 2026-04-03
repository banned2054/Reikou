using Reikou.Models.Enums;
using System.Runtime.InteropServices;

namespace Reikou.Models.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvEvent
{
    public MpvEventId eventId;
    public int        error;
    public ulong      replyUserData;
    public nint       data;
}
