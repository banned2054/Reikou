using Reikou.Models.Enums;
using System.Runtime.InteropServices;

namespace Reikou.Models.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvEventProperty
{
    public string    name;
    public MpvFormat format;
    public nint      data;
}
