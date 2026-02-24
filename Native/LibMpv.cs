using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using TestMpv.Enums;
using TestMpv.Structs;

namespace TestMpv.Native;

public static partial class LibMpv
{
    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_create")]
    private static partial nint MpvCreate();

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_create_client", StringMarshalling = StringMarshalling.Utf8)]
    private static partial nint MpvCreateClient(nint mpvHandle, string command);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_initialize")]
    private static partial MpvStatus MpvInitialize(nint mpvHandle);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_destroy")]
    private static partial void MpvDestroy(nint mpvHandle);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_command")]
    private static partial MpvStatus MpvCommand(nint mpvHandle, nint strings);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_command_string", StringMarshalling = StringMarshalling.Utf8)]
    private static partial MpvStatus MpvCommandString(nint mpvHandle, string command);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_command_ret")]
    private static partial MpvStatus MpvCommandRet(nint mpvHandle, nint strings, nint node);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_free_node_contents")]
    private static partial void MpvFreeNodeContents(nint node);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_error_string")]
    private static partial nint MpvErrorString(MpvStatus status);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_request_log_messages", StringMarshalling = StringMarshalling.Utf8)]
    private static partial MpvStatus MpvRequestLogMessages(nint mpvHandle, string minLevel);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_set_option", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int MpvSetOption(nint mpvHandle, string name, MpvFormat format, ref long data);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_set_option", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int MpvSetOption(nint mpvHandle, string name, MpvFormat format, ref double data);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_set_option_string", StringMarshalling = StringMarshalling.Utf8)]
    private static partial int MpvSetOptionString(nint mpvHandle, string name, string value);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_get_property", StringMarshalling = StringMarshalling.Utf8)]
    private static partial MpvStatus MpvGetProperty(nint mpvHandle, string name, MpvFormat format, out nint data);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_get_property", StringMarshalling = StringMarshalling.Utf8)]
    private static partial MpvStatus MpvGetProperty(nint mpvHandle, string name, MpvFormat format, out int data);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_get_property", StringMarshalling = StringMarshalling.Utf8)]
    private static partial MpvStatus MpvGetProperty(nint mpvHandle, string name, MpvFormat format, out long data);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_get_property", StringMarshalling = StringMarshalling.Utf8)]
    private static partial MpvStatus MpvGetProperty(nint mpvHandle, string name, MpvFormat format, out double data);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_set_property", StringMarshalling = StringMarshalling.Utf8)]
    private static partial MpvStatus MpvSetProperty(nint mpvHandle, string name, MpvFormat format, ref string data);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_set_property", StringMarshalling = StringMarshalling.Utf8)]
    private static partial MpvStatus MpvSetProperty(nint mpvHandle, string name, MpvFormat format, ref long data);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_set_property", StringMarshalling = StringMarshalling.Utf8)]
    private static partial MpvStatus MpvSetProperty(nint mpvHandle, string name, MpvFormat format, ref double data);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_observe_property", StringMarshalling = StringMarshalling.Utf8)]
    private static partial MpvStatus MpvObserveProperty(nint      mpvHandle, ulong replyUserdata, string name,
                                                        MpvFormat format);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_unobserve_property")]
    private static partial int MpvUnobserveProperty(nint mpvHandle, ulong registeredReplyUserdata);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_free")]
    private static partial void MpvFree(nint data);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_wait_event")]
    private static partial nint MpvWaitEvent(nint mpvHandle, double timeout);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_request_event")]
    private static partial MpvStatus MpvRequestEvent(nint mpvHandle, MpvEventId id, int enable);

    // --- 这里恢复原版，使用 ReadOnlySpan ---
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_render_context_create")]
    private static partial MpvStatus MpvRenderContextCreate(out nint                     res, nint mpvHandle,
                                                            ReadOnlySpan<MpvRenderParam> paramsArray);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_render_context_set_parameter")]
    private static partial MpvStatus MpvRenderContextSetParameter(nint ctx, MpvRenderParam param);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_render_context_get_info")]
    private static partial MpvStatus MpvRenderContextGetInfo(nint ctx, MpvRenderParam param);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_render_context_set_update_callback")]
    private static partial void MpvRenderContextSetUpdateCallback(nint ctx, MpvRenderUpdateFn callback,
                                                                  nint callbackCtx);

    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_render_context_free")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    private static partial void MpvRenderContextFree(nint ctx);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_render_context_render")]
    private static partial MpvStatus MpvRenderContextRender(nint ctx, ReadOnlySpan<MpvRenderParam> paramsArray);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void MpvRenderUpdateFn(nint callbackContext);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate nint MpvGetProcAddressFn(nint ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    [LibraryImport("libmpv-2.dll", EntryPoint = "mpv_render_context_update")]
    private static partial MpvRenderUpdateFlag MpvRenderContextUpdate(nint ctx);


    // --- 包装方法 ---
    public static nint      Create()                                      => MpvCreate();
    public static nint      CreateClient(nint  mpvHandle, string command) => MpvCreateClient(mpvHandle, command);
    public static MpvStatus Initialize(nint    mpvHandle)                 => MpvInitialize(mpvHandle);
    public static void      Destroy(nint       mpvHandle)                 => MpvDestroy(mpvHandle);
    public static MpvStatus Command(nint       mpvHandle, nint   strings) => MpvCommand(mpvHandle, strings);
    public static MpvStatus CommandString(nint mpvHandle, string command) => MpvCommandString(mpvHandle, command);

    public static MpvStatus CommandRet(nint mpvHandle, nint strings, nint node) =>
        MpvCommandRet(mpvHandle, strings, node);

    public static void FreeNodeContents(nint node)   => MpvFreeNodeContents(node);
    public static nint ErrorString(MpvStatus status) => MpvErrorString(status);

    public static MpvStatus RequestLogMessages(nint mpvHandle, string minLevel) =>
        MpvRequestLogMessages(mpvHandle, minLevel);

    public static int SetOption(nint mpvHandle, string name, MpvFormat format, ref long data) =>
        MpvSetOption(mpvHandle, name, format, ref data);

    public static int SetOption(nint mpvHandle, string name, MpvFormat format, ref double data) =>
        MpvSetOption(mpvHandle, name, format, ref data);

    public static int SetOptionString(nint mpvHandle, string name, string value) =>
        MpvSetOptionString(mpvHandle, name, value);

    public static MpvStatus GetProperty(nint mpvHandle, string name, MpvFormat format, out nint data) =>
        MpvGetProperty(mpvHandle, name, format, out data);

    public static MpvStatus GetProperty(nint handle, string name, MpvFormat format, out int data) =>
        MpvGetProperty(handle, name, format, out data);

    public static MpvStatus GetProperty(nint handle, string name, MpvFormat format, out long data) =>
        MpvGetProperty(handle, name, format, out data);

    public static MpvStatus GetProperty(nint mpvHandle, string name, MpvFormat format, out double data) =>
        MpvGetProperty(mpvHandle, name, format, out data);

    public static MpvStatus SetProperty(nint mpvHandle, string name, MpvFormat format, ref string data) =>
        MpvSetProperty(mpvHandle, name, format, ref data);

    public static MpvStatus SetProperty(nint mpvHandle, string name, MpvFormat format, ref long data) =>
        MpvSetProperty(mpvHandle, name, format, ref data);

    public static MpvStatus SetProperty(nint mpvHandle, string name, MpvFormat format, ref double data) =>
        MpvSetProperty(mpvHandle, name, format, ref data);

    public static MpvStatus ObserveProperty(nint mpvHandle, ulong replyUserdata, string name, MpvFormat format) =>
        MpvObserveProperty(mpvHandle, replyUserdata, name, format);

    public static int UnobserveProperty(nint mpvHandle, ulong registeredReplyUserdata) =>
        MpvUnobserveProperty(mpvHandle, registeredReplyUserdata);

    public static void Free(nint      data)                      => MpvFree(data);
    public static nint WaitEvent(nint mpvHandle, double timeout) => MpvWaitEvent(mpvHandle, timeout);

    public static MpvStatus RequestEvent(nint mpvHandle, MpvEventId id, int enable) =>
        MpvRequestEvent(mpvHandle, id, enable);

    public static MpvStatus
        RenderContextCreate(out nint res, nint mpvHandle, ReadOnlySpan<MpvRenderParam> paramsArray) =>
        MpvRenderContextCreate(out res, mpvHandle, paramsArray);

    public static MpvStatus RenderContextSetParameter(nint ctx, MpvRenderParam param) =>
        MpvRenderContextSetParameter(ctx, param);

    public static MpvStatus RenderContextGetInfo(nint ctx, MpvRenderParam param) => MpvRenderContextGetInfo(ctx, param);

    public static void RenderContextSetUpdateCallback(nint ctx, MpvRenderUpdateFn callback, nint callbackCtx) =>
        MpvRenderContextSetUpdateCallback(ctx, callback, callbackCtx);

    public static void RenderContextFree(nint ctx) => MpvRenderContextFree(ctx);

    public static MpvStatus RenderContextRender(nint ctx, ReadOnlySpan<MpvRenderParam> paramsArray) =>
        MpvRenderContextRender(ctx, paramsArray);

    public static MpvRenderUpdateFlag RenderContextUpdate(nint ctx) => MpvRenderContextUpdate(ctx);

    public static string[] ConvertFromUtf8Strings(nint utf8StringArray, int stringCount)
    {
        var intPtrArray = new nint[stringCount];
        var stringArray = new string[stringCount];
        Marshal.Copy(utf8StringArray, intPtrArray, 0, stringCount);
        for (var i = 0; i < stringCount; i++) stringArray[i] = ConvertFromUtf8(intPtrArray[i]);
        return stringArray;
    }

    public static string ConvertFromUtf8(nint nativeUtf8)
    {
        var len = 0;
        while (Marshal.ReadByte(nativeUtf8, len) != 0) ++len;
        var buffer = new byte[len];
        Marshal.Copy(nativeUtf8, buffer, 0, buffer.Length);
        return Encoding.UTF8.GetString(buffer);
    }

    public static string GetError(MpvStatus  err) => ConvertFromUtf8(ErrorString(err));
    public static byte[] GetUtf8Bytes(string s)   => Encoding.UTF8.GetBytes(s + "\0");
}
