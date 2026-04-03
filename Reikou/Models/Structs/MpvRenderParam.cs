using Reikou.Models.Enums;
using System.Runtime.InteropServices;

namespace Reikou.Models.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvRenderParam
{
    public MpvRenderParamType type;
    public nint               data;
}
