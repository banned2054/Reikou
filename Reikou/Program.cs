using Avalonia;
using Reikou.Native;
using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Reikou;

internal class Program
{
    static Program()
    {
        NativeLibrary.SetDllImportResolver(typeof(LibMpv).Assembly, ResolveLibMpv);
    }

    [STAThread]
    public static void Main(string[] args)
    {
        // 1. 初始化 Serilog
        Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console()
                    .WriteTo.File("logs/myapp-.log", rollingInterval : RollingInterval.Day)
                    .CreateLogger();
        try
        {
            Log.Information("应用正在启动...");
            BuildAvaloniaApp()
               .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "应用崩溃了！");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
                                                             .UsePlatformDetect()
                                                             .WithInterFont()
                                                             .LogToTrace()
                                                             .UseSkia();

    private static IntPtr ResolveLibMpv(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != LibMpv.MpvLibraryName) return IntPtr.Zero;

        foreach (var path in GetLibMpvCandidates())
            if (File.Exists(path) && NativeLibrary.TryLoad(path, out var handle))
                return handle;

        return IntPtr.Zero;
    }

    private static string[] GetLibMpvCandidates()
    {
        var baseDir = AppContext.BaseDirectory;
        var rid     = GetCurrentRid();
        var names   = GetPlatformLibMpvNames();

        return names.SelectMany(name => new[]
        {
            Path.Combine(baseDir, "Libs", rid, name),
            Path.Combine(baseDir, "runtimes", rid, "native", name),
            Path.Combine(baseDir, "Libs", name),
            Path.Combine(baseDir, name)
        }).ToArray();
    }

    private static string GetCurrentRid()
    {
        var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "win"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "osx"
                : "linux";

        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "arm64",
            Architecture.Arm   => "arm",
            Architecture.X86   => "x86",
            _                  => "x64"
        };

        return $"{os}-{arch}";
    }

    private static string[] GetPlatformLibMpvNames()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return ["libmpv-2.dll", "mpv.dll"];

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return ["libmpv.2.dylib", "libmpv.dylib", "mpv.dylib"];

        return ["libmpv.so.2", "libmpv.so", "mpv.so"];
    }
}
