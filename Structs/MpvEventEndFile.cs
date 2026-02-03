using System.Runtime.InteropServices;

namespace TestMpv.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvEventEndFile
{
    public int reason;
    public int error;
}
