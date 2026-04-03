using Reikou.Models.Enums;
using System.Runtime.InteropServices;

namespace Reikou.Models.Structs;

[StructLayout(LayoutKind.Explicit, Size = 16)]
public struct MpvNode
{
    [FieldOffset(0)]
    public nint str;

    [FieldOffset(0)]
    public int flag;

    [FieldOffset(0)]
    public long int64;

    [FieldOffset(0)]
    public double dbl;

    [FieldOffset(0)]
    public nint list;

    [FieldOffset(0)]
    public nint ba;

    [FieldOffset(8)]
    public MpvFormat format;
}
