using Reikou.Models.Enums;
using System.Runtime.InteropServices;

namespace Reikou.Models.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvEventLogMessage
{
    public nint        prefix;
    public nint        level;
    public nint        text;
    public MpvLogLevel logLevel;
}
