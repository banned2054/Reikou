using System.Runtime.InteropServices;

namespace TestMpv.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct MpvOpenglFbo
{
    public int fbo;
    public int width;
    public int heigh;
    public int internalFormat; // 0 for default
}
