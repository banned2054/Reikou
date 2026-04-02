using System.Runtime.InteropServices;
using TestMpv.Models.Enums;

namespace TestMpv.Models.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvEventProperty
{
    public string    name;
    public MpvFormat format;
    public nint      data;
}
