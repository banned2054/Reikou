using System.Runtime.InteropServices;
using TestMpv.Models.Enums;

namespace TestMpv.Models.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvRenderParam
{
    public MpvRenderParamType type;
    public nint               data;
}
