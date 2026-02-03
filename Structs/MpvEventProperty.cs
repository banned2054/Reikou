using System.Runtime.InteropServices;
using TestMpv.Enums;

namespace TestMpv.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvEventProperty
{
    public string    name;
    public MpvFormat format;
    public nint      data;
}
