using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Reikou.ViewModels;
using System.Linq;

namespace Reikou.Presentation.Views.Settings;

public partial class SettingsPanel : UserControl
{
    public SettingsPanel()
    {
        InitializeComponent();
    }

    private void NumberStepUp(object? sender, RoutedEventArgs e) => StepNumber(sender, 1);

    private void NumberStepDown(object? sender, RoutedEventArgs e) => StepNumber(sender, -1);

    private static void StepNumber(object? sender, int direction)
    {
        if (sender is not RepeatButton { CommandParameter: NumberSettingViewModel setting }) return;

        // NumericValue 的 setter 内部已完成取整与范围限制
        setting.NumericValue = (setting.NumericValue ?? 0m) + (decimal)setting.Increment * direction;
    }

    private async void BrowsePath(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: PathSettingViewModel setting } ||
            TopLevel.GetTopLevel(this) is not { } topLevel)
            return;

        if (setting.PathKind == SettingPathKind.Directory)
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title         = setting.Title,
                AllowMultiple = false
            });
            var path = folders.FirstOrDefault()?.Path.LocalPath;

            if (!string.IsNullOrWhiteSpace(path)) setting.Value = path;

            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title         = setting.Title,
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

        if (!string.IsNullOrWhiteSpace(filePath)) setting.Value = filePath;
    }
}
