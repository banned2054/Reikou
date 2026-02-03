using System.Runtime.InteropServices;
using TestMpv.Enums;

namespace TestMpv.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvRenderParam
{
    public MpvRenderParamType type;
    public nint               data;
}
