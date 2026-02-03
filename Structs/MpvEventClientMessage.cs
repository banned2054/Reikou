using System.Runtime.InteropServices;

namespace TestMpv.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvEventClientMessage
{
    public int  num_args;
    public nint args;
}
