using System.Runtime.InteropServices;
using TestMpv.Models.Enums;

namespace TestMpv.Models.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvEventLogMessage
{
    public nint        prefix;
    public nint        level;
    public nint        text;
    public MpvLogLevel logLevel;
}
