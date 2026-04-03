using System.Runtime.InteropServices;

namespace Reikou.Models.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvEventEndFile
{
    public int reason;
    public int error;
}
