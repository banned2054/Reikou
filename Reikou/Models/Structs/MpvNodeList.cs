using System.Runtime.InteropServices;

namespace Reikou.Models.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvNodeList
{
    public int  Num;
    public nint Values;
    public nint Keys;
}
