using System;
using System.IO;
using System.Text.Json.Serialization;
using static Reikou.Models.Settings.SettingsNormalization;

namespace Reikou.Models.Settings;

public enum HardwareDecodingMode
{
    Automatic,
    Disabled,
    Compatibility,
    D3D11Copy,
    Dxva2Copy,
    NvdecCopy,
    CudaCopy,
    VaapiCopy,
    VideoToolboxCopy
}

public enum VideoSynchronizationMode
{
    Audio,
    DisplayResample,
    DisplayResampleVideoDrop,
    DisplayResampleDesync,
    DisplayVideoDrop,
    DisplayAudioDrop,
    DisplayDesync,
    Desync
}

public enum VideoScalingFilter
{
    Bilinear,
    Bicubic,
    Spline36,
    Lanczos,
    EwaLanczos,
    EwaLanczosSharp,
    Mitchell,
    CatmullRom,
    Oversample
}

public enum DitherTargetDepth
{
    Disabled,
    Automatic,
    EightBit,
    TenBit
}

public enum TemporalScalingFilter
{
    Bilinear,
    Bicubic,
    Spline16,
    Mitchell,
    Oversample
}

public enum ToneMappingAlgorithm
{
    Automatic,
    Clip,
    Mobius,
    Reinhard,
    Hable,
    Bt2390,
    Gamma,
    Linear,
    Spline,
    Bt2446A
}

public enum AudioOutputDriver
{
    Automatic,
    Wasapi,
    PipeWire,
    PulseAudio,
    CoreAudio,
    OpenAl,
    Sdl
}

public enum ExternalFileMatchMode
{
    Disabled,
    Exact,
    Fuzzy,
    All
}

public enum SubtitleBlendMode
{
    Disabled,
    Video,
    Always
}

public enum ScreenshotImageFormat
{
    Jpeg,
    Png,
    WebP,
    JpegXl
}

public enum ApplicationThemeMode
{
    System,
    Dark,
    Light
}

public sealed record PlaybackSettings
{
    public double DefaultSpeed          { get; init; } = 1;
    public bool   RememberPosition      { get; init; } = true;
    public bool   KeepOpen              { get; init; } = true;
    public bool   KeepOpenPaused        { get; init; }
    public bool   LoopFile              { get; init; }
    public bool   LoopPlaylist          { get; init; }
    public bool   HighResolutionSeeking { get; init; } = true;
    public bool   CacheEnabled          { get; init; } = true;
    public int    CacheSizeMiB          { get; init; } = 150;

    public PlaybackSettings Normalize() => this with
    {
        DefaultSpeed = ClampFinite(DefaultSpeed, 0.25, 4, 1),
        CacheSizeMiB = Math.Clamp(CacheSizeMiB, 16, 8192)
    };
}

public sealed record VideoSettings
{
    public HardwareDecodingMode     HardwareDecoding       { get; init; } = HardwareDecodingMode.Automatic;
    public VideoSynchronizationMode Synchronization        { get; init; } = VideoSynchronizationMode.Audio;
    public bool                     Interpolation          { get; init; }
    public bool                     PreserveInterpolation  { get; init; } = true;
    public TemporalScalingFilter    TemporalScaling        { get; init; } = TemporalScalingFilter.Mitchell;
    public VideoScalingFilter       Upscaling              { get; init; } = VideoScalingFilter.Lanczos;
    public VideoScalingFilter       ChromaScaling          { get; init; } = VideoScalingFilter.Bilinear;
    public VideoScalingFilter       Downscaling            { get; init; } = VideoScalingFilter.Lanczos;
    public bool                     CorrectDownscaling     { get; init; }
    public bool                     SigmoidUpscaling       { get; init; }
    public bool                     Deband                 { get; init; }
    public DitherTargetDepth        DitherDepth            { get; init; } = DitherTargetDepth.Automatic;
    public bool                     HdrComputePeak         { get; init; } = true;
    public bool                     AllowDelayedPeakDetect { get; init; } = true;

    public VideoSettings Normalize() => this with
    {
        HardwareDecoding = NormalizeEnum(HardwareDecoding, HardwareDecodingMode.Automatic),
        Synchronization = NormalizeEnum(Synchronization, VideoSynchronizationMode.Audio),
        TemporalScaling = NormalizeEnum(TemporalScaling, TemporalScalingFilter.Mitchell),
        Upscaling = NormalizeEnum(Upscaling, VideoScalingFilter.Lanczos),
        ChromaScaling = NormalizeEnum(ChromaScaling, VideoScalingFilter.Bilinear),
        Downscaling = NormalizeEnum(Downscaling, VideoScalingFilter.Lanczos),
        DitherDepth = NormalizeEnum(DitherDepth, DitherTargetDepth.Automatic)
    };
}

public sealed record AudioSettings
{
    public AudioOutputDriver     OutputDriver       { get; init; } = AudioOutputDriver.Automatic;
    public string                Device             { get; init; } = string.Empty;
    public string                PreferredLanguages { get; init; } = string.Empty;
    public ExternalFileMatchMode ExternalFileMatch  { get; init; } = ExternalFileMatchMode.Disabled;
    public double                StartupVolume      { get; init; } = 100;
    public double                MaximumVolume      { get; init; } = 130;
    public bool                  RememberVolume     { get; init; } = true;
    public double                DelaySeconds       { get; init; }

    public AudioSettings Normalize() => this with
    {
        OutputDriver = NormalizeEnum(OutputDriver, AudioOutputDriver.Automatic),
        ExternalFileMatch = NormalizeEnum(ExternalFileMatch, ExternalFileMatchMode.Disabled),
        Device = Device?.Trim()                         ?? string.Empty,
        PreferredLanguages = PreferredLanguages?.Trim() ?? string.Empty,
        StartupVolume = ClampFinite(StartupVolume, 0, 1000, 100),
        MaximumVolume = ClampFinite(MaximumVolume, 100, 1000, 130),
        DelaySeconds = ClampFinite(DelaySeconds, -100, 100, 0)
    };
}

public sealed record SubtitleSettings
{
    public string                PreferredLanguages { get; init; } = string.Empty;
    public ExternalFileMatchMode AutoLoad           { get; init; } = ExternalFileMatchMode.Fuzzy;
    public string                SearchPaths        { get; init; } = "sub;subtitles;字幕";
    public SubtitleBlendMode     BlendMode          { get; init; } = SubtitleBlendMode.Disabled;
    public string                FontFamily         { get; init; } = "sans-serif";
    public double                FontSize           { get; init; } = 55;
    public string                TextColor          { get; init; } = "#FFFFFFFF";
    public string                BorderColor        { get; init; } = "#FF000000";
    public string                BackgroundColor    { get; init; } = "#00000000";
    public double                BorderSize         { get; init; } = 3;
    public double                ShadowOffset       { get; init; }
    public double                VerticalPosition   { get; init; } = 100;
    public double                DelaySeconds       { get; init; }

    public SubtitleSettings Normalize() => this with
    {
        AutoLoad = NormalizeEnum(AutoLoad, ExternalFileMatchMode.Fuzzy),
        BlendMode = NormalizeEnum(BlendMode, SubtitleBlendMode.Disabled),
        PreferredLanguages = PreferredLanguages?.Trim() ?? string.Empty,
        SearchPaths = SearchPaths?.Trim()               ?? string.Empty,
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? "sans-serif" : FontFamily.Trim(),
        FontSize = ClampFinite(FontSize, 8, 160, 55),
        TextColor = NormalizeColor(TextColor, "#FFFFFFFF"),
        BorderColor = NormalizeColor(BorderColor, "#FF000000"),
        BackgroundColor = NormalizeColor(BackgroundColor, "#00000000"),
        BorderSize = ClampFinite(BorderSize, 0, 20, 3),
        ShadowOffset = ClampFinite(ShadowOffset, 0, 20, 0),
        VerticalPosition = ClampFinite(VerticalPosition, 0, 150, 100),
        DelaySeconds = ClampFinite(DelaySeconds, -100, 100, 0)
    };
}

public sealed record ScreenshotSettings
{
    public string                Directory        { get; init; } = GetDefaultDirectory();
    public string                FileNameTemplate { get; init; } = "Reikou-%Y%m%d-%H%M%S";
    public ScreenshotImageFormat Format           { get; init; } = ScreenshotImageFormat.Png;
    public int                   JpegQuality      { get; init; } = 95;
    public int                   PngCompression   { get; init; } = 5;
    public int                   PngFilter        { get; init; } = 5;
    public bool                  TagColorSpace    { get; init; } = true;
    public bool                  HighBitDepth     { get; init; }
    public bool                  JpegSourceChroma { get; init; } = true;

    public ScreenshotSettings Normalize() => this with
    {
        Directory = string.IsNullOrWhiteSpace(Directory) ? GetDefaultDirectory() : Directory.Trim(),
        FileNameTemplate = string.IsNullOrWhiteSpace(FileNameTemplate)
            ? "Reikou-%Y%m%d-%H%M%S"
            : FileNameTemplate.Trim(),
        Format = NormalizeEnum(Format, ScreenshotImageFormat.Png),
        JpegQuality = Math.Clamp(JpegQuality, 0, 100),
        PngCompression = Math.Clamp(PngCompression, 0, 9),
        PngFilter = Math.Clamp(PngFilter, 0, 5)
    };

    private static string GetDefaultDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Reikou");
}

public sealed record ColorManagementSettings
{
    public bool                 IccProfileAuto   { get; init; } = true;
    public string               IccProfilePath   { get; init; } = string.Empty;
    public int                  IccLutSize       { get; init; } = 64;
    public int                  IccIntent        { get; init; } = 1;
    public double               IccForceContrast { get; init; }
    public ToneMappingAlgorithm ToneMapping      { get; init; } = ToneMappingAlgorithm.Automatic;
    public double               TargetPeakNits   { get; init; }

    public ColorManagementSettings Normalize() => this with
    {
        IccProfilePath = IccProfilePath?.Trim() ?? string.Empty,
        IccLutSize = Math.Clamp(IccLutSize, 2, 512),
        IccIntent = Math.Clamp(IccIntent, 0, 3),
        IccForceContrast = ClampFinite(IccForceContrast, 0, 1000000, 0),
        ToneMapping = NormalizeEnum(ToneMapping, ToneMappingAlgorithm.Automatic),
        TargetPeakNits = ClampFinite(TargetPeakNits, 0, 10000, 0)
    };
}

public sealed record DanmakuSettings
{
    public bool   Enabled   { get; init; } = true;
    public double Opacity   { get; init; } = 1;
    public double FontScale { get; init; } = 1;
    public double Speed     { get; init; } = 1;
    public int    Density   { get; init; } = 100;

    public DanmakuSettings Normalize() => this with
    {
        Opacity = ClampFinite(Opacity, 0.1, 1, 1),
        FontScale = ClampFinite(FontScale, 0.5, 2, 1),
        Speed = ClampFinite(Speed, 0.5, 2, 1),
        Density = Math.Clamp(Density, 10, 100)
    };
}

public sealed record InterfaceSettings
{
    public ApplicationThemeMode Theme             { get; init; } = ApplicationThemeMode.System;
    public double               OverlayHideDelay  { get; init; } = 1;
    public bool                 RememberWindow    { get; init; } = true;
    public bool                 StartInFullscreen { get; init; }
    public int?                 WindowX           { get; init; }
    public int?                 WindowY           { get; init; }
    public double?              WindowWidth       { get; init; }
    public double?              WindowHeight      { get; init; }
    public bool                 WindowMaximized   { get; init; }

    public InterfaceSettings Normalize() => this with
    {
        Theme = NormalizeEnum(Theme, ApplicationThemeMode.System),
        OverlayHideDelay = ClampFinite(OverlayHideDelay, 0.5, 10, 1),
        WindowWidth = WindowWidth is { } width
            ? ClampFinite(width, 640, 10000, 1280)
            : null,
        WindowHeight = WindowHeight is { } height
            ? ClampFinite(height, 360, 10000, 720)
            : null
    };
}

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 2;

    public int                     SchemaVersion { get; init; } = CurrentSchemaVersion;
    public PlaybackSettings        Playback      { get; init; } = new();
    public VideoSettings           Video         { get; init; } = new();
    public AudioSettings           Audio         { get; init; } = new();
    public SubtitleSettings        Subtitle      { get; init; } = new();
    public ScreenshotSettings      Screenshot    { get; init; } = new();
    public ColorManagementSettings Color         { get; init; } = new();
    public DanmakuSettings         Danmaku       { get; init; } = new();
    public InterfaceSettings       Interface     { get; init; } = new();

    public AppSettings Normalize() => this with
    {
        SchemaVersion = CurrentSchemaVersion,
        Playback = (Playback).Normalize(),
        Video = (Video).Normalize(),
        Audio = (Audio).Normalize(),
        Subtitle = (Subtitle).Normalize(),
        Screenshot = (Screenshot).Normalize(),
        Color = (Color).Normalize(),
        Danmaku = (Danmaku).Normalize(),
        Interface = (Interface).Normalize()
    };
}

internal static class SettingsNormalization
{
    public static T NormalizeEnum<T>(T value, T fallback) where T : struct, Enum =>
        Enum.IsDefined(value) ? value : fallback;

    public static double ClampFinite(double value, double minimum, double maximum, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;

    public static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var candidate = value.Trim();
        if (candidate.Length is not (7 or 9) || candidate[0] != '#')
        {
            return fallback;
        }

        for (var index = 1; index < candidate.Length; index++)
        {
            if (!Uri.IsHexDigit(candidate[index]))
            {
                return fallback;
            }
        }

        return candidate.ToUpperInvariant();
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal partial class AppSettingsJsonContext : JsonSerializerContext;
