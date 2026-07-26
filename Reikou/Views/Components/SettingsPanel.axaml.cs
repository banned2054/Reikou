using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Reikou.ViewModels;
using System.Linq;

namespace Reikou.Views.Components;

public partial class SettingsPanel : UserControl
{
    public SettingsPanel()
    {
        InitializeComponent();
    }

    private async void BrowsePath(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: PathSettingViewModel setting } ||
            TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            return;
        }

        if (setting.PathKind == SettingPathKind.Directory)
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = setting.Title,
                AllowMultiple = false
            });
            var path = folders.FirstOrDefault()?.Path.LocalPath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                setting.Value = path;
            }

            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = setting.Title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("ICC 配置文件")
                {
                    Patterns = ["*.icc", "*.icm"]
                },
                FilePickerFileTypes.All
            ]
        });
        var filePath = files.FirstOrDefault()?.Path.LocalPath;
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            setting.Value = filePath;
        }
    }
}
