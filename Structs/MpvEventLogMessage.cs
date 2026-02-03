using System.Runtime.InteropServices;
using TestMpv.Enums;

namespace TestMpv.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvEventLogMessage
{
    public nint        prefix;
    public nint        level;
    public nint        text;
    public MpvLogLevel logLevel;
}
