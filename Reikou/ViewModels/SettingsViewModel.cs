using Reikou.Models.Settings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Reikou.ViewModels;

internal sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly HardwareDecodingMode _initialHardwareDecoding;
    private readonly AudioOutputDriver    _initialAudioOutput;
    private readonly string               _initialAudioDevice;

    private SettingsCategoryViewModel _selectedCategory;
    private AppSettings               _settings;

    private bool    _isOpen;
    private string? _activeHardwareDecoder;
    private string  _searchText = string.Empty;

    public SettingsViewModel(AppSettings settings)
    {
        _settings                = settings.Normalize();
        _initialHardwareDecoding = _settings.Video.HardwareDecoding;
        _initialAudioOutput      = _settings.Audio.OutputDriver;
        _initialAudioDevice      = _settings.Audio.Device;

        Categories        = BuildCategories();
        _selectedCategory = Categories[0];
        RefreshVisibleOptions();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<AppSettings>?         SettingsChanged;

    public ObservableCollection<SettingsCategoryViewModel> Categories     { get; }
    public ObservableCollection<SettingsOptionViewModel>   VisibleOptions { get; } = [];

    public bool HasVisibleOptions => VisibleOptions.Count > 0;

    public bool IsOpen
    {
        get => _isOpen;
        set => SetField(ref _isOpen, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value ?? string.Empty))
            {
                RefreshVisibleOptions();
            }
        }
    }

    public SettingsCategoryViewModel SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (value != null && SetField(ref _selectedCategory, value))
            {
                RefreshVisibleOptions();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContentTitle)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContentDescription)));
            }
        }
    }

    public string ContentTitle => string.IsNullOrWhiteSpace(SearchText) ? SelectedCategory.Title : "搜索结果";

    public string ContentDescription => string.IsNullOrWhiteSpace(SearchText)
        ? SelectedCategory.Description
        : $"匹配“{SearchText.Trim()}”的设置";

    public bool IsRestartRequired =>
        _settings.Video.HardwareDecoding != _initialHardwareDecoding ||
        _settings.Audio.OutputDriver     != _initialAudioOutput      ||
        !string.Equals(_settings.Audio.Device, _initialAudioDevice, StringComparison.Ordinal);

    public string ActiveHardwareDecoderText => _activeHardwareDecoder switch
    {
        null or "" => "等待媒体",
        "no"       => "软件解码",
        _          => _activeHardwareDecoder
    };

    public void SetActiveHardwareDecoder(string? decoder)
    {
        if (SetField(ref _activeHardwareDecoder, decoder, nameof(ActiveHardwareDecoderText)))
        {
            RefreshReadOnlyValues();
        }
    }

    public void SetDanmakuEnabled(bool enabled)
    {
        var option = Categories.SelectMany(category => category.Options)
                               .OfType<ToggleSettingViewModel>()
                               .FirstOrDefault(item => item.Title == "默认显示弹幕");
        if (option != null)
        {
            option.Value = enabled;
        }
    }

    private ObservableCollection<SettingsCategoryViewModel> BuildCategories() =>
    [
        new("播放", "播放流程、进度恢复、循环和缓存", BuildPlaybackOptions()),
        new("视频", "硬件解码、同步、缩放和画面处理", BuildVideoOptions()),
        new("音频", "输出设备、默认音量、语言和延迟", BuildAudioOptions()),
        new("字幕", "自动加载、语言、字体、颜色和位置", BuildSubtitleOptions()),
        new("截图", "保存位置、命名和图像编码", BuildScreenshotOptions()),
        new("色彩与 HDR", "ICC 色彩管理和 HDR 到 SDR 色调映射", BuildColorOptions()),
        new("弹幕", "弹幕显示、密度、速度和可读性", BuildDanmakuOptions()),
        new("界面", "主题、控制栏行为和固定渲染边界", BuildInterfaceOptions())
    ];

    private IEnumerable<SettingsOptionViewModel> BuildPlaybackOptions()
    {
        var value = _settings.Playback;
        return
        [
            Number("默认播放速度", "新媒体开始播放时使用的速度。", value.DefaultSpeed, 0.25, 4, 0.05, "×",
                   v => Update(s => s with { Playback = s.Playback with { DefaultSpeed = v } })),
            Toggle("记住播放位置", "退出时保存进度，并在下次打开同一媒体时恢复。", value.RememberPosition,
                   v => Update(s => s with { Playback = s.Playback with { RememberPosition = v } })),
            Toggle("播放结束后保留画面", "媒体结束后停留在最后一帧。", value.KeepOpen,
                   v => Update(s => s with { Playback = s.Playback with { KeepOpen = v } })),
            Toggle("保留画面时暂停", "播放结束后同时进入暂停状态。", value.KeepOpenPaused,
                   v => Update(s => s with { Playback = s.Playback with { KeepOpenPaused = v } })),
            Toggle("单曲循环", "循环播放当前媒体。", value.LoopFile,
                   v => Update(s => s with { Playback = s.Playback with { LoopFile = v } })),
            Toggle("列表循环", "播放列表到达末尾后从头继续。", value.LoopPlaylist,
                   v => Update(s => s with { Playback = s.Playback with { LoopPlaylist = v } })),
            Toggle("高精度定位", "Seek 时优先定位到准确时间点。", value.HighResolutionSeeking,
                   v => Update(s => s with { Playback = s.Playback with { HighResolutionSeeking = v } })),
            Toggle("启用媒体缓存", "为本地和网络媒体启用 mpv 缓存。", value.CacheEnabled,
                   v => Update(s => s with { Playback = s.Playback with { CacheEnabled = v } })),
            Number("最大缓存", "mpv 解复用缓存的内存上限。", value.CacheSizeMiB, 16, 8192, 16, "MiB",
                   v => Update(s => s with { Playback = s.Playback with { CacheSizeMiB = (int)v } }))
        ];
    }

    private IEnumerable<SettingsOptionViewModel> BuildVideoOptions()
    {
        var value = _settings.Video;
        return
        [
            Choice("硬件解码", "自动优先使用显卡解码；兼容复制会把解码结果复制回系统内存。",
                   HardwareDecodingChoices, (int)value.HardwareDecoding,
                   v => Update(s => s with
                   {
                       Video = s.Video with { HardwareDecoding = (HardwareDecodingMode)v }
                   }), true),
            new ReadOnlySettingViewModel("当前解码器", "当前媒体实际使用的 mpv 解码路径。", ActiveHardwareDecoderText),
            Choice("视频同步", "选择音视频时钟和显示刷新率之间的同步策略。",
                   VideoSynchronizationChoices, (int)value.Synchronization,
                   v => Update(s => s with
                   {
                       Video = s.Video with { Synchronization = (VideoSynchronizationMode)v }
                   })),
            Toggle("插值", "显示器重采样同步时生成过渡帧，减少刷新率不匹配造成的抖动。", value.Interpolation,
                   v => Update(s => s with { Video = s.Video with { Interpolation = v } })),
            Toggle("保留插值结果", "Seek 后短暂保留已有插值结果，降低画面恢复时的跳变。", value.PreserveInterpolation,
                   v => Update(s => s with { Video = s.Video with { PreserveInterpolation = v } })),
            Choice("时间缩放算法", "插值开启时用于生成时间维度过渡帧的算法。", TemporalScalingChoices,
                   (int)value.TemporalScaling,
                   v => Update(s => s with
                   {
                       Video = s.Video with { TemporalScaling = (TemporalScalingFilter)v }
                   })),
            Choice("放大算法", "视频放大时使用的 GPU 采样算法。", ScalingChoices, (int)value.Upscaling,
                   v => Update(s => s with { Video = s.Video with { Upscaling = (VideoScalingFilter)v } })),
            Choice("色度算法", "重建色度平面时使用的 GPU 采样算法。", ScalingChoices, (int)value.ChromaScaling,
                   v => Update(s => s with { Video = s.Video with { ChromaScaling = (VideoScalingFilter)v } })),
            Choice("缩小算法", "视频缩小时使用的 GPU 采样算法。", ScalingChoices, (int)value.Downscaling,
                   v => Update(s => s with { Video = s.Video with { Downscaling = (VideoScalingFilter)v } })),
            Toggle("修正缩小采样", "缩小时扩大卷积滤镜范围，提高细节稳定性。", value.CorrectDownscaling,
                   v => Update(s => s with { Video = s.Video with { CorrectDownscaling = v } })),
            Toggle("S 形放大", "放大前使用 S 形颜色变换以减轻锐利振铃。", value.SigmoidUpscaling,
                   v => Update(s => s with { Video = s.Video with { SigmoidUpscaling = v } })),
            Toggle("去色带", "减少渐变中的条带和量化痕迹，会增加少量 GPU 开销。", value.Deband,
                   v => Update(s => s with { Video = s.Video with { Deband = v } })),
            Choice("抖动输出位深", "控制 GPU renderer 的抖动目标位深。", DitherChoices, (int)value.DitherDepth,
                   v => Update(s => s with { Video = s.Video with { DitherDepth = (DitherTargetDepth)v } })),
            Toggle("HDR 峰值检测", "动态估计 HDR 内容峰值，用于更稳定的色调映射。", value.HdrComputePeak,
                   v => Update(s => s with { Video = s.Video with { HdrComputePeak = v } })),
            Toggle("延迟峰值检测", "允许峰值检测延迟一帧以提高结果稳定性。", value.AllowDelayedPeakDetect,
                   v => Update(s => s with { Video = s.Video with { AllowDelayedPeakDetect = v } }))
        ];
    }

    private IEnumerable<SettingsOptionViewModel> BuildAudioOptions()
    {
        var value = _settings.Audio;
        return
        [
            Choice("音频输出", "自动会选择当前平台的推荐输出驱动。", AudioOutputChoices, (int)value.OutputDriver,
                   v => Update(s => s with { Audio = s.Audio with { OutputDriver = (AudioOutputDriver)v } }), true),
            Text("音频设备", "留空使用系统默认设备；设备标识由 mpv 的 audio-device-list 提供。", value.Device,
                 v => Update(s => s with { Audio = s.Audio with { Device = v } }), true),
            Text("首选语言", "按优先顺序填写语言代码，使用逗号分隔，例如 jpn,zh,eng。", value.PreferredLanguages,
                 v => Update(s => s with { Audio = s.Audio with { PreferredLanguages = v } })),
            Choice("外挂音轨匹配", "自动加载与视频文件名匹配的外挂音轨。", ExternalMatchChoices,
                   (int)value.ExternalFileMatch,
                   v => Update(s => s with
                   {
                       Audio = s.Audio with { ExternalFileMatch = (ExternalFileMatchMode)v }
                   })),
            Number("启动音量", "播放器启动时使用的默认音量。", value.StartupVolume, 0, 1000, 1, "%",
                   v => Update(s => s with { Audio = s.Audio with { StartupVolume = v } })),
            Number("最大音量", "允许控制栏调节到的音量上限；超过 100% 会进行软件增益。", value.MaximumVolume,
                   100, 1000, 5, "%",
                   v => Update(s => s with { Audio = s.Audio with { MaximumVolume = v } })),
            Toggle("记住音量", "退出时把当前音量保存为下次启动音量。", value.RememberVolume,
                   v => Update(s => s with { Audio = s.Audio with { RememberVolume = v } })),
            Number("音频延迟", "正值让音频相对视频延后。", value.DelaySeconds, -10, 10, 0.05, "秒",
                   v => Update(s => s with { Audio = s.Audio with { DelaySeconds = v } }))
        ];
    }

    private IEnumerable<SettingsOptionViewModel> BuildSubtitleOptions()
    {
        var value = _settings.Subtitle;
        return
        [
            Text("首选语言", "按优先顺序填写语言代码，使用逗号分隔，例如 zh,chs,cht,eng。", value.PreferredLanguages,
                 v => Update(s => s with { Subtitle = s.Subtitle with { PreferredLanguages = v } })),
            Choice("自动加载字幕", "设置视频同目录下字幕文件的名称匹配策略。", ExternalMatchChoices, (int)value.AutoLoad,
                   v => Update(s => s with { Subtitle = s.Subtitle with { AutoLoad = (ExternalFileMatchMode)v } })),
            Text("字幕搜索目录", "使用分号分隔视频目录下的相对路径。", value.SearchPaths,
                 v => Update(s => s with { Subtitle = s.Subtitle with { SearchPaths = v } })),
            Choice("字幕混合", "控制字幕在缩放前混入视频还是在最终画面上绘制。", SubtitleBlendChoices,
                   (int)value.BlendMode,
                   v => Update(s => s with { Subtitle = s.Subtitle with { BlendMode = (SubtitleBlendMode)v } })),
            Text("字体", "填写系统字体族名称。", value.FontFamily,
                 v => Update(s => s with { Subtitle = s.Subtitle with { FontFamily = v } })),
            Number("字号", "ASS 样式未指定字体大小时使用。", value.FontSize, 8, 160, 1, "px",
                   v => Update(s => s with { Subtitle = s.Subtitle with { FontSize = v } })),
            Text("文字颜色", "使用 #AARRGGBB 格式。", value.TextColor,
                 v => Update(s => s with { Subtitle = s.Subtitle with { TextColor = v } })),
            Text("描边颜色", "使用 #AARRGGBB 格式。", value.BorderColor,
                 v => Update(s => s with { Subtitle = s.Subtitle with { BorderColor = v } })),
            Text("背景颜色", "使用 #AARRGGBB 格式。", value.BackgroundColor,
                 v => Update(s => s with { Subtitle = s.Subtitle with { BackgroundColor = v } })),
            Number("描边宽度", "字幕轮廓的粗细。", value.BorderSize, 0, 20, 0.5, "px",
                   v => Update(s => s with { Subtitle = s.Subtitle with { BorderSize = v } })),
            Number("阴影偏移", "字幕阴影相对文字的偏移量。", value.ShadowOffset, 0, 20, 0.5, "px",
                   v => Update(s => s with { Subtitle = s.Subtitle with { ShadowOffset = v } })),
            Number("垂直位置", "100 表示默认底部位置，较小的值会向上移动。", value.VerticalPosition, 0, 150, 1, "%",
                   v => Update(s => s with { Subtitle = s.Subtitle with { VerticalPosition = v } })),
            Number("默认字幕延迟", "正值让字幕相对视频延后。", value.DelaySeconds, -10, 10, 0.05, "秒",
                   v => Update(s => s with { Subtitle = s.Subtitle with { DelaySeconds = v } }))
        ];
    }

    private IEnumerable<SettingsOptionViewModel> BuildScreenshotOptions()
    {
        var value = _settings.Screenshot;
        return
        [
            Path("保存目录", "截图文件保存位置。", value.Directory, SettingPathKind.Directory,
                 v => Update(s => s with { Screenshot = s.Screenshot with { Directory = v } })),
            Text("文件名模板", "支持 mpv 的 strftime 占位符，例如 %Y%m%d-%H%M%S。", value.FileNameTemplate,
                 v => Update(s => s with { Screenshot = s.Screenshot with { FileNameTemplate = v } })),
            Choice("图像格式", "选择截图编码格式。", ScreenshotFormatChoices, (int)value.Format,
                   v => Update(s => s with { Screenshot = s.Screenshot with { Format = (ScreenshotImageFormat)v } })),
            Number("JPEG 质量", "仅用于 JPEG 截图。", value.JpegQuality, 0, 100, 1, "%",
                   v => Update(s => s with { Screenshot = s.Screenshot with { JpegQuality = (int)v } })),
            Number("PNG 压缩", "较高等级生成更小的文件，但耗时更长。", value.PngCompression, 0, 9, 1, "",
                   v => Update(s => s with { Screenshot = s.Screenshot with { PngCompression = (int)v } })),
            Choice("PNG 滤镜", "选择 PNG 压缩前使用的行滤镜策略。", PngFilterChoices, value.PngFilter,
                   v => Update(s => s with { Screenshot = s.Screenshot with { PngFilter = v } })),
            Toggle("写入色彩空间", "在支持的格式中写入源画面的色彩空间信息。", value.TagColorSpace,
                   v => Update(s => s with { Screenshot = s.Screenshot with { TagColorSpace = v } })),
            Toggle("高位深截图", "允许支持的格式保存高于 8 bit 的图像。", value.HighBitDepth,
                   v => Update(s => s with { Screenshot = s.Screenshot with { HighBitDepth = v } })),
            Toggle("JPEG 使用源色度", "JPEG 编码时尽量沿用视频源的色度采样。", value.JpegSourceChroma,
                   v => Update(s => s with { Screenshot = s.Screenshot with { JpegSourceChroma = v } }))
        ];
    }

    private IEnumerable<SettingsOptionViewModel> BuildColorOptions()
    {
        var value = _settings.Color;
        return
        [
            Toggle("自动 ICC 配置", "尝试使用当前显示器的 ICC 色彩配置。", value.IccProfileAuto,
                   v => Update(s => s with { Color = s.Color with { IccProfileAuto = v } })),
            Path("ICC 配置文件", "手动指定 ICC 文件；自动模式关闭时使用。", value.IccProfilePath, SettingPathKind.File,
                 v => Update(s => s with { Color = s.Color with { IccProfilePath = v } })),
            Number("ICC 3D LUT 尺寸", "较大的 LUT 更精确，但初始化和 GPU 成本更高。", value.IccLutSize, 2, 512, 2, "",
                   v => Update(s => s with { Color = s.Color with { IccLutSize = (int)v } })),
            Choice("ICC 渲染意图", "选择感知、相对色度、饱和度或绝对色度。", IccIntentChoices, value.IccIntent,
                   v => Update(s => s with { Color = s.Color with { IccIntent = v } })),
            Number("ICC 强制对比度", "覆盖 ICC 文件中的静态对比度；0 表示不覆盖。", value.IccForceContrast,
                   0, 1000000, 100, "",
                   v => Update(s => s with { Color = s.Color with { IccForceContrast = v } })),
            Choice("HDR 色调映射", "把 HDR 内容映射到当前 SDR OpenGL 输出。", ToneMappingChoices,
                   (int)value.ToneMapping,
                   v => Update(s => s with { Color = s.Color with { ToneMapping = (ToneMappingAlgorithm)v } })),
            Number("目标峰值亮度", "0 表示由 mpv 自动估计；非零值使用指定的显示峰值。", value.TargetPeakNits,
                   0, 10000, 10, "nit",
                   v => Update(s => s with { Color = s.Color with { TargetPeakNits = v } }))
        ];
    }

    private IEnumerable<SettingsOptionViewModel> BuildDanmakuOptions()
    {
        var value = _settings.Danmaku;
        return
        [
            Toggle("默认显示弹幕", "打开媒体和加载弹幕源后默认显示弹幕。", value.Enabled,
                   v => Update(s => s with { Danmaku = s.Danmaku with { Enabled = v } })),
            Number("透明度", "控制整个弹幕图层的透明度。", value.Opacity, 0.1, 1, 0.05, "",
                   v => Update(s => s with { Danmaku = s.Danmaku with { Opacity = v } })),
            Number("字号比例", "相对弹幕源字号的缩放比例。", value.FontScale, 0.5, 2, 0.05, "×",
                   v => Update(s => s with { Danmaku = s.Danmaku with { FontScale = v } })),
            Number("移动速度", "相对默认弹幕运动速度的倍率。", value.Speed, 0.5, 2, 0.05, "×",
                   v => Update(s => s with { Danmaku = s.Danmaku with { Speed = v } })),
            Number("显示区域", "限制弹幕使用的垂直屏幕区域，减少对画面的遮挡。", value.Density, 10, 100, 5, "%",
                   v => Update(s => s with { Danmaku = s.Danmaku with { Density = (int)v } }))
        ];
    }

    private IEnumerable<SettingsOptionViewModel> BuildInterfaceOptions()
    {
        var value = _settings.Interface;
        return
        [
            Choice("主题", "跟随系统，或固定使用深色/浅色主题。", ThemeChoices, (int)value.Theme,
                   v => Update(s => s with { Interface = s.Interface with { Theme = (ApplicationThemeMode)v } })),
            Number("控制栏隐藏延迟", "鼠标停止操作后，等待多久隐藏控制栏和指针。", value.OverlayHideDelay,
                   0.5, 10, 0.5, "秒",
                   v => Update(s => s with { Interface = s.Interface with { OverlayHideDelay = v } })),
            Toggle("记住窗口状态", "保存桌面窗口的位置和大小。", value.RememberWindow,
                   v => Update(s => s with { Interface = s.Interface with { RememberWindow = v } })),
            Toggle("启动时全屏", "创建播放器窗口后直接进入全屏。", value.StartInFullscreen,
                   v => Update(s => s with { Interface = s.Interface with { StartInFullscreen = v } })),
            new ReadOnlySettingViewModel("视频输出", "由嵌入式播放器渲染链路固定。", "libmpv render API"),
            new ReadOnlySettingViewModel("图形接口", "当前桌面渲染宿主固定使用 OpenGL。", "OpenGL（固定）"),
            new ReadOnlySettingViewModel("mpv 配置", "不读取外部 mpv.conf，所有设置由 Reikou 管理。", "内部管理")
        ];
    }

    private void RefreshVisibleOptions()
    {
        VisibleOptions.Clear();
        var search = SearchText.Trim();
        var source = string.IsNullOrEmpty(search)
            ? SelectedCategory.Options
            : Categories.SelectMany(category => category.Options)
                        .Where(option => option.Matches(search));

        foreach (var option in source)
        {
            VisibleOptions.Add(option);
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasVisibleOptions)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContentTitle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ContentDescription)));
    }

    private void RefreshReadOnlyValues()
    {
        foreach (var category in Categories)
        {
            for (var index = 0; index < category.Options.Count; index++)
            {
                if (category.Options[index] is ReadOnlySettingViewModel readOnly &&
                    readOnly.Title == "当前解码器")
                {
                    category.Options[index] =
                        new ReadOnlySettingViewModel(readOnly.Title, readOnly.Description, ActiveHardwareDecoderText);
                }
            }
        }

        RefreshVisibleOptions();
    }

    private void Update(Func<AppSettings, AppSettings> update)
    {
        _settings = update(_settings).Normalize();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRestartRequired)));
        SettingsChanged?.Invoke(_settings);
    }

    private static ToggleSettingViewModel Toggle(string title, string description, bool value, Action<bool> changed,
                                                 bool   restartRequired = false) =>
        new(title, description, value, changed, restartRequired);

    private static ChoiceSettingViewModel Choice(string title, string description, IEnumerable<SettingChoice> choices,
                                                 int    selected, Action<int> changed, bool restartRequired = false) =>
        new(title, description, choices, selected, changed, restartRequired);

    private static NumberSettingViewModel Number(string         title, string description, double value, double minimum,
                                                 double         maximum, double increment, string suffix,
                                                 Action<double> changed, bool restartRequired = false) =>
        new(title, description, value, minimum, maximum, increment, suffix, changed, restartRequired);

    private static TextSettingViewModel Text(string title, string description, string value, Action<string> changed,
                                             bool   restartRequired = false) =>
        new(title, description, value, changed, restartRequired);

    private static PathSettingViewModel Path(string title, string description, string value, SettingPathKind pathKind,
                                             Action<string> changed, bool restartRequired = false) =>
        new(title, description, value, pathKind, changed, restartRequired);

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private static readonly SettingChoice[] HardwareDecodingChoices =
    [
        new("自动（推荐）", (int)HardwareDecodingMode.Automatic),
        new("关闭", (int)HardwareDecodingMode.Disabled),
        new("自动复制（兼容）", (int)HardwareDecodingMode.Compatibility),
        new("D3D11VA 复制（Windows）", (int)HardwareDecodingMode.D3D11Copy),
        new("DXVA2 复制（Windows）", (int)HardwareDecodingMode.Dxva2Copy),
        new("NVDEC 复制（NVIDIA）", (int)HardwareDecodingMode.NvdecCopy),
        new("CUDA 复制（NVIDIA）", (int)HardwareDecodingMode.CudaCopy),
        new("VA-API 复制（Linux）", (int)HardwareDecodingMode.VaapiCopy),
        new("VideoToolbox 复制（macOS）", (int)HardwareDecodingMode.VideoToolboxCopy)
    ];

    private static readonly SettingChoice[] VideoSynchronizationChoices =
    [
        new("音频时钟", (int)VideoSynchronizationMode.Audio),
        new("显示器重采样", (int)VideoSynchronizationMode.DisplayResample),
        new("显示器重采样 + 丢帧", (int)VideoSynchronizationMode.DisplayResampleVideoDrop),
        new("显示器重采样 + 解同步", (int)VideoSynchronizationMode.DisplayResampleDesync),
        new("显示器时钟 + 视频丢帧", (int)VideoSynchronizationMode.DisplayVideoDrop),
        new("显示器时钟 + 音频丢帧", (int)VideoSynchronizationMode.DisplayAudioDrop),
        new("显示器时钟 + 解同步", (int)VideoSynchronizationMode.DisplayDesync),
        new("完全解同步", (int)VideoSynchronizationMode.Desync)
    ];

    private static readonly SettingChoice[] ScalingChoices =
    [
        new("双线性（最快）", (int)VideoScalingFilter.Bilinear),
        new("双三次", (int)VideoScalingFilter.Bicubic),
        new("Spline 36", (int)VideoScalingFilter.Spline36),
        new("Lanczos", (int)VideoScalingFilter.Lanczos),
        new("EWA Lanczos", (int)VideoScalingFilter.EwaLanczos),
        new("EWA Lanczos Sharp", (int)VideoScalingFilter.EwaLanczosSharp),
        new("Mitchell", (int)VideoScalingFilter.Mitchell),
        new("Catmull-Rom", (int)VideoScalingFilter.CatmullRom),
        new("Oversample", (int)VideoScalingFilter.Oversample)
    ];

    private static readonly SettingChoice[] DitherChoices =
    [
        new("关闭", (int)DitherTargetDepth.Disabled),
        new("自动", (int)DitherTargetDepth.Automatic),
        new("8 bit", (int)DitherTargetDepth.EightBit),
        new("10 bit", (int)DitherTargetDepth.TenBit)
    ];

    private static readonly SettingChoice[] TemporalScalingChoices =
    [
        new("双线性", (int)TemporalScalingFilter.Bilinear),
        new("双三次", (int)TemporalScalingFilter.Bicubic),
        new("Spline 16", (int)TemporalScalingFilter.Spline16),
        new("Mitchell（推荐）", (int)TemporalScalingFilter.Mitchell),
        new("Oversample", (int)TemporalScalingFilter.Oversample)
    ];

    private static readonly SettingChoice[] AudioOutputChoices =
    [
        new("自动（推荐）", (int)AudioOutputDriver.Automatic),
        new("WASAPI（Windows）", (int)AudioOutputDriver.Wasapi),
        new("PipeWire（Linux）", (int)AudioOutputDriver.PipeWire),
        new("PulseAudio（Linux）", (int)AudioOutputDriver.PulseAudio),
        new("CoreAudio（macOS）", (int)AudioOutputDriver.CoreAudio),
        new("OpenAL", (int)AudioOutputDriver.OpenAl),
        new("SDL", (int)AudioOutputDriver.Sdl)
    ];

    private static readonly SettingChoice[] ExternalMatchChoices =
    [
        new("关闭", (int)ExternalFileMatchMode.Disabled),
        new("精确匹配", (int)ExternalFileMatchMode.Exact),
        new("模糊匹配", (int)ExternalFileMatchMode.Fuzzy),
        new("全部", (int)ExternalFileMatchMode.All)
    ];

    private static readonly SettingChoice[] SubtitleBlendChoices =
    [
        new("关闭（推荐）", (int)SubtitleBlendMode.Disabled),
        new("随视频缩放", (int)SubtitleBlendMode.Video),
        new("始终混合", (int)SubtitleBlendMode.Always)
    ];

    private static readonly SettingChoice[] ScreenshotFormatChoices =
    [
        new("PNG", (int)ScreenshotImageFormat.Png),
        new("JPEG", (int)ScreenshotImageFormat.Jpeg),
        new("WebP", (int)ScreenshotImageFormat.WebP),
        new("JPEG XL", (int)ScreenshotImageFormat.JpegXl)
    ];

    private static readonly SettingChoice[] IccIntentChoices =
    [
        new("感知", 0),
        new("相对色度", 1),
        new("饱和度", 2),
        new("绝对色度", 3)
    ];

    private static readonly SettingChoice[] PngFilterChoices =
    [
        new("无", 0),
        new("Sub", 1),
        new("Up", 2),
        new("Average", 3),
        new("Paeth", 4),
        new("混合（推荐）", 5)
    ];

    private static readonly SettingChoice[] ToneMappingChoices =
    [
        new("自动", (int)ToneMappingAlgorithm.Automatic),
        new("Clip", (int)ToneMappingAlgorithm.Clip),
        new("Mobius", (int)ToneMappingAlgorithm.Mobius),
        new("Reinhard", (int)ToneMappingAlgorithm.Reinhard),
        new("Hable", (int)ToneMappingAlgorithm.Hable),
        new("BT.2390", (int)ToneMappingAlgorithm.Bt2390),
        new("Gamma", (int)ToneMappingAlgorithm.Gamma),
        new("Linear", (int)ToneMappingAlgorithm.Linear),
        new("Spline", (int)ToneMappingAlgorithm.Spline),
        new("BT.2446A", (int)ToneMappingAlgorithm.Bt2446A)
    ];

    private static readonly SettingChoice[] ThemeChoices =
    [
        new("跟随系统", (int)ApplicationThemeMode.System),
        new("深色", (int)ApplicationThemeMode.Dark),
        new("浅色", (int)ApplicationThemeMode.Light)
    ];
}
