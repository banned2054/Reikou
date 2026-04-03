using Reikou.Native;
using System.Runtime.InteropServices;

namespace Reikou.Models.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvOpenglInitParams
{
    public LibMpv.MpvGetProcAddressFn getProcAddress;
    public nint                getProcAddressContext;
    public nint                extraExtensions;
}
