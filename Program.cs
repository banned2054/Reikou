using Avalonia;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Serilog;

namespace TestMpv;

internal class Program
{
    static Program()
    {
        NativeLibrary.SetDllImportResolver(typeof(Native.LibMpv).Assembly, ResolveLibMpv);
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
        if (libraryName == "libmpv-2.dll")
        {
            var baseDir = AppContext.BaseDirectory;
            var path    = Path.Combine(baseDir, "Libs", "libmpv-2.dll");
            if (File.Exists(path))
            {
                return NativeLibrary.Load(path);
            }
        }

        // 让系统按默认方式继续找
        return IntPtr.Zero;
    }
}
