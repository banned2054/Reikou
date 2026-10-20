using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace Reikou.Native;

internal static partial class LibMediaInfo
{
    private const string MediaInfoLibraryName = "MediaInfo";

    public static MediaInfoHandle Create() => new(MediaInfoNew());

    public static bool Open(MediaInfoHandle handle, string filePath)
    {
        var opened = MediaInfoOpen(handle, filePath) != 0;
        if (opened) handle.MarkOpened();

        return opened;
    }

    public static string GetGeneral(MediaInfoHandle handle, string parameter)
    {
        var value = MediaInfoGet(handle, MediaInfoStreamKind.General, 0, parameter, MediaInfoInfoKind.Text,
                                 MediaInfoInfoKind.Name);

        return value == nint.Zero ? string.Empty : Marshal.PtrToStringUni(value) ?? string.Empty;
    }

    [LibraryImport(MediaInfoLibraryName, EntryPoint = "MediaInfo_New")]
    private static partial nint MediaInfoNew();

    [LibraryImport(MediaInfoLibraryName, EntryPoint = "MediaInfo_Open", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nuint MediaInfoOpen(MediaInfoHandle handle, string filePath);

    [LibraryImport(MediaInfoLibraryName, EntryPoint = "MediaInfo_Get", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint MediaInfoGet(MediaInfoHandle handle, MediaInfoStreamKind streamKind, nuint streamNumber,
                                             string parameter, MediaInfoInfoKind infoKind,
                                             MediaInfoInfoKind searchKind);

    [LibraryImport(MediaInfoLibraryName, EntryPoint = "MediaInfo_Close")]
    private static partial void MediaInfoClose(nint handle);

    [LibraryImport(MediaInfoLibraryName, EntryPoint = "MediaInfo_Delete")]
    private static partial void MediaInfoDelete(nint handle);

    internal sealed class MediaInfoHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private bool _opened;

        public MediaInfoHandle(nint handle) : base(true)
        {
            SetHandle(handle);
        }

        public void MarkOpened()
        {
            _opened = true;
        }

        protected override bool ReleaseHandle()
        {
            if (_opened) MediaInfoClose(handle);

            MediaInfoDelete(handle);
            return true;
        }
    }

    private enum MediaInfoStreamKind
    {
        General
    }

    private enum MediaInfoInfoKind
    {
        Name,
        Text
    }
}
