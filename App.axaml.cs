using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System;

namespace TestMpv;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            desktop.MainWindow = mainWindow;

            var args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                var filePath = args[1];
                // 在窗口加载完成后自动播放
                mainWindow.Loaded += async (_, _) => { await mainWindow.Player.LoadFileAsync(filePath); };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
