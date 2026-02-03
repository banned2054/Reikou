using System.Runtime.InteropServices;
using TestMpv.Native;

namespace TestMpv.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvOpenglInitParams
{
    public LibMpv.MpvGetProcAddressFn getProcAddress;
    public nint                getProcAddressContext;
    public nint                extraExtensions;
}
