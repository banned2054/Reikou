using Microsoft.Extensions.Logging;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TestMpv.Enums;
using TestMpv.Native;
using TestMpv.Structs;

namespace TestMpv.Services;

public class MpvService : IDisposable
{
    private readonly ILogger<MpvService> _logger;
    private          nint                _handle;
    private          nint                _renderContext;
    private          bool                _disposed;

    // 防止委托被 GC 回收
    private LibMpv.MpvGetProcAddressFn? _getProcAddressDelegate;
    private LibMpv.MpvRenderUpdateFn?   _updateCallbackDelegate;

    public MpvService(ILogger<MpvService> logger)
    {
        _logger = logger;
        _handle = LibMpv.Create();
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create mpv instance.");

        var status = LibMpv.Initialize(_handle);
        if (status < 0)
            HandleError(status, "Initialize failed");
    }

    public void InitializeOpenGl(Func<string, nint> getProcAddress, Action updateCallback)
    {
        CheckDisposed();

        // 1. 包装委托
        _getProcAddressDelegate = (ctx, name) => getProcAddress(name);
        _updateCallbackDelegate = _ => updateCallback();

        // 2. 准备初始化参数 (使用 Marshal 分配内存，这是原版最稳妥的做法)
        var glInitParams = new MpvOpenglInitParams
        {
            getProcAddress        = _getProcAddressDelegate,
            getProcAddressContext = nint.Zero
        };

        var glInitParamsPtr = Marshal.AllocHGlobal(Marshal.SizeOf(glInitParams));
        Marshal.StructureToPtr(glInitParams, glInitParamsPtr, false);

        // 3. 准备 API 字符串
        var apiBytes = LibMpv.GetUtf8Bytes("opengl");
        var apiPtr   = Marshal.AllocHGlobal(apiBytes.Length);
        Marshal.Copy(apiBytes, 0, apiPtr, apiBytes.Length);

        try
        {
            // 直接传递数组，让 LibraryImport 处理 Marshalling
            var renderParams = new MpvRenderParam[]
            {
                new() { type = MpvRenderParamType.ApiType, data          = apiPtr },
                new() { type = MpvRenderParamType.OpenglInitParams, data = glInitParamsPtr },
                new() { type = MpvRenderParamType.Invalid, data          = nint.Zero }
            };

            var err = LibMpv.RenderContextCreate(out _renderContext, _handle, renderParams);
            if (err < 0)
            {
                throw new Exception($"Failed to create mpv render context: {LibMpv.GetError(err)}");
            }
        }
        finally
        {
            // 清理手动分配的非托管内存
            Marshal.FreeHGlobal(glInitParamsPtr);
            Marshal.FreeHGlobal(apiPtr);
        }

        // 4. 设置回调
        LibMpv.RenderContextSetUpdateCallback(_renderContext, _updateCallbackDelegate, nint.Zero);
    }

    public void Render(int fbo, int width, int height)
    {
        if (_disposed || _renderContext == IntPtr.Zero) return;

        var fboParam = new MpvOpenglFbo
        {
            fbo            = fbo,
            width          = width,
            heigh          = height,
            internalFormat = 0
        };

        int flipY = 1;

        // 这里原版使用了 unsafe + stackalloc，这部分通常是安全的，保留原版写法
        unsafe
        {
            var renderParams = stackalloc MpvRenderParam[4];

            renderParams[0].type = MpvRenderParamType.OpenglFbo;
            renderParams[0].data = (nint)(&fboParam);

            renderParams[1].type = MpvRenderParamType.FlipY;
            renderParams[1].data = (nint)(&flipY);

            renderParams[2].type = MpvRenderParamType.Invalid;
            renderParams[2].data = nint.Zero;

            var paramsSpan = new ReadOnlySpan<MpvRenderParam>(renderParams, 3);
            LibMpv.RenderContextRender(_renderContext, paramsSpan);
        }
    }

    public void Command(string command)
    {
        if (_disposed) return;
        Task.Run(() =>
        {
            var err = LibMpv.CommandString(_handle, command);
            if (err < 0) HandleError(err, $"Command failed: {command}");
        });
    }

    public T? GetProperty<T>(string name)
    {
        CheckDisposed();

        // 1. 字符串类型处理
        if (typeof(T) == typeof(string))
        {
            var err = LibMpv.GetProperty(_handle, name, MpvFormat.String, out IntPtr lpBuffer);
            if (err < 0) return default;
            var result = LibMpv.ConvertFromUtf8(lpBuffer);
            LibMpv.Free(lpBuffer);
            return (T)(object)result;
        }

        // 2. 布尔类型处理 (这是修复的关键)
        // 必须显式使用 MpvFormat.Flag，否则 MPV 可能无法正确返回 pause 状态
        if (typeof(T) == typeof(bool))
        {
            var err = LibMpv.GetProperty(_handle, name, MpvFormat.Flag, out int val);
            if (err >= 0) return (T)(object)(val != 0);
            return default;
        }

        // 3. 整数/长整数处理
        if (typeof(T) == typeof(long) || typeof(T) == typeof(int))
        {
            var err = LibMpv.GetProperty(_handle, name, MpvFormat.Long, out long val);
            if (err < 0) return default;

            if (typeof(T) == typeof(int))
                return (T)(object)(int)val;
            return (T)(object)val;
        }

        // 4. 浮点数处理
        if (typeof(T) == typeof(double))
        {
            var err = LibMpv.GetProperty(_handle, name, MpvFormat.Double, out double val);
            if (err >= 0) return (T)(object)val;
            return default;
        }

        throw new NotSupportedException($"Type {typeof(T)} is not supported by MpvService.");
    }

    public void SetProperty<T>(string name, T value)
    {
        CheckDisposed();
        MpvStatus err = MpvStatus.Success;

        switch (value)
        {
            case string s :
                err = LibMpv.SetProperty(_handle, name, MpvFormat.String, ref s);
                break;
            case bool b :
                long lBool = b ? 1 : 0;
                err = LibMpv.SetProperty(_handle, name, MpvFormat.Flag, ref lBool);
                break;
            case int i :
                long lInt = i;
                err = LibMpv.SetProperty(_handle, name, MpvFormat.Long, ref lInt);
                break;
            case long l :
                err = LibMpv.SetProperty(_handle, name, MpvFormat.Long, ref l);
                break;
            case double d :
                err = LibMpv.SetProperty(_handle, name, MpvFormat.Double, ref d);
                break;
            default :
                throw new NotSupportedException($"Type {typeof(T)} not supported");
        }

        if (err < 0) HandleError(err, $"SetProperty {name}={value}");
    }

    private void HandleError(MpvStatus err, string msg)
    {
        if (err >= 0) return;
        _logger.LogError("{Msg}: {Error}", msg, LibMpv.GetError(err));
    }

    private void CheckDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(MpvService));
    }

    public void Dispose()
    {
        if (_disposed) return;

        if (_renderContext != IntPtr.Zero)
        {
            LibMpv.RenderContextFree(_renderContext);
            _renderContext = IntPtr.Zero;
        }

        if (_handle != IntPtr.Zero)
        {
            LibMpv.Destroy(_handle);
            _handle = IntPtr.Zero;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
