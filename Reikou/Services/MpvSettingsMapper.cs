using Reikou.Models.Settings;
using System.Collections.Generic;
using System.Globalization;

namespace Reikou.Services;

internal static class MpvSettingsMapper
{
    private static readonly HashSet<string> RestartRequiredOptions =
    [
        "hwdec",
        "ao",
        "audio-device"
    ];

    public static IEnumerable<KeyValuePair<string, string>> GetOptions(AppSettings appSettings)
    {
        var settings   = appSettings.Normalize();
        var playback   = settings.Playback;
        var video      = settings.Video;
        var audio      = settings.Audio;
        var subtitle   = settings.Subtitle;
        var screenshot = settings.Screenshot;
        var color      = settings.Color;

        yield return Pair("speed", Number(playback.DefaultSpeed));
        // The managed history is the sole owner of resume position.
        yield return Pair("save-position-on-quit", "no");
        yield return Pair("resume-playback", "no");
        yield return Pair("write-filename-in-watch-later-config", "no");
        yield return Pair("keep-open", Flag(playback.KeepOpen));
        yield return Pair("keep-open-pause", Flag(playback.KeepOpenPaused));
        yield return Pair("loop-file", playback.LoopFile ? "inf" : "no");
        yield return Pair("loop-playlist", "no");
        yield return Pair("hr-seek", Flag(playback.HighResolutionSeeking));
        yield return Pair("cache", Flag(playback.CacheEnabled));
        yield return Pair("demuxer-max-bytes", $"{playback.CacheSizeMiB}MiB");

        yield return Pair("hwdec", HardwareDecoding(video.HardwareDecoding));
        yield return Pair("video-sync", VideoSynchronization(video.Synchronization));
        yield return Pair("interpolation", Flag(video.Interpolation && SupportsInterpolation(video.Synchronization)));
        yield return Pair("interpolation-preserve", Flag(video.PreserveInterpolation));
        yield return Pair("tscale", TemporalScaling(video.TemporalScaling));
        yield return Pair("scale", Scaling(video.Upscaling));
        yield return Pair("cscale", Scaling(video.ChromaScaling));
        yield return Pair("dscale", Scaling(video.Downscaling));
        yield return Pair("correct-downscaling", Flag(video.CorrectDownscaling));
        yield return Pair("sigmoid-upscaling", Flag(video.SigmoidUpscaling));
        yield return Pair("deband", Flag(video.Deband));
        yield return Pair("dither-depth", DitherDepth(video.DitherDepth));
        yield return Pair("hdr-compute-peak", Flag(video.HdrComputePeak));
        yield return Pair("allow-delayed-peak-detect", Flag(video.AllowDelayedPeakDetect));

        if (audio.OutputDriver != AudioOutputDriver.Automatic) yield return Pair("ao", AudioOutput(audio.OutputDriver));

        if (!string.IsNullOrWhiteSpace(audio.Device)) yield return Pair("audio-device", audio.Device);

        if (!string.IsNullOrWhiteSpace(audio.PreferredLanguages)) yield return Pair("alang", audio.PreferredLanguages);

        yield return Pair("audio-file-auto", ExternalMatch(audio.ExternalFileMatch));
        yield return Pair("volume", Number(audio.StartupVolume));
        yield return Pair("volume-max", Number(audio.MaximumVolume));
        yield return Pair("audio-delay", Number(audio.DelaySeconds));

        if (!string.IsNullOrWhiteSpace(subtitle.PreferredLanguages))
            yield return Pair("slang", subtitle.PreferredLanguages);

        yield return Pair("sub-auto", ExternalMatch(subtitle.AutoLoad));
        if (!string.IsNullOrWhiteSpace(subtitle.SearchPaths)) yield return Pair("sub-file-paths", subtitle.SearchPaths);

        yield return Pair("blend-subtitles", SubtitleBlend(subtitle.BlendMode));
        yield return Pair("sub-font", subtitle.FontFamily);
        yield return Pair("sub-font-size", Number(subtitle.FontSize));
        yield return Pair("sub-color", subtitle.TextColor);
        yield return Pair("sub-border-color", subtitle.BorderColor);
        yield return Pair("sub-back-color", subtitle.BackgroundColor);
        yield return Pair("sub-border-size", Number(subtitle.BorderSize));
        yield return Pair("sub-shadow-offset", Number(subtitle.ShadowOffset));
        yield return Pair("sub-pos", Number(subtitle.VerticalPosition));
        yield return Pair("sub-delay", Number(subtitle.DelaySeconds));

        yield return Pair("screenshot-directory", screenshot.Directory);
        yield return Pair("screenshot-template", screenshot.FileNameTemplate);
        yield return Pair("screenshot-format", ScreenshotFormat(screenshot.Format));
        yield return Pair("screenshot-jpeg-quality", screenshot.JpegQuality.ToString(CultureInfo.InvariantCulture));
        yield return Pair("screenshot-png-compression",
                          screenshot.PngCompression.ToString(CultureInfo.InvariantCulture));
        yield return Pair("screenshot-png-filter", screenshot.PngFilter.ToString(CultureInfo.InvariantCulture));
        yield return Pair("screenshot-tag-colorspace", Flag(screenshot.TagColorSpace));
        yield return Pair("screenshot-high-bit-depth", Flag(screenshot.HighBitDepth));
        yield return Pair("screenshot-jpeg-source-chroma", Flag(screenshot.JpegSourceChroma));

        yield return Pair("icc-profile-auto", Flag(color.IccProfileAuto));
        if (!string.IsNullOrWhiteSpace(color.IccProfilePath)) yield return Pair("icc-profile", color.IccProfilePath);

        yield return Pair("icc-3dlut-size", $"{color.IccLutSize}x{color.IccLutSize}x{color.IccLutSize}");
        yield return Pair("icc-intent", color.IccIntent.ToString(CultureInfo.InvariantCulture));
        yield return Pair("icc-force-contrast",
                          color.IccForceContrast <= 0 ? "no" : Number(color.IccForceContrast));
        yield return Pair("tone-mapping", ToneMapping(color.ToneMapping));
        yield return Pair("target-peak", color.TargetPeakNits <= 0 ? "auto" : Number(color.TargetPeakNits));
    }

    public static bool RequiresRestart(string optionName) => RestartRequiredOptions.Contains(optionName);

    public static bool SupportsInterpolation(VideoSynchronizationMode mode) =>
        mode is VideoSynchronizationMode.DisplayResample
             or VideoSynchronizationMode.DisplayResampleVideoDrop
             or VideoSynchronizationMode.DisplayResampleDesync;

    private static KeyValuePair<string, string> Pair(string name, string value) => new(name, value);

    private static string Flag(bool value) => value ? "yes" : "no";

    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static string HardwareDecoding(HardwareDecodingMode mode) => mode switch
    {
        HardwareDecodingMode.Disabled         => "no",
        HardwareDecodingMode.Compatibility    => "auto-copy",
        HardwareDecodingMode.D3D11Copy        => "d3d11va-copy",
        HardwareDecodingMode.Dxva2Copy        => "dxva2-copy",
        HardwareDecodingMode.NvdecCopy        => "nvdec-copy",
        HardwareDecodingMode.CudaCopy         => "cuda-copy",
        HardwareDecodingMode.VaapiCopy        => "vaapi-copy",
        HardwareDecodingMode.VideoToolboxCopy => "videotoolbox-copy",
        _                                     => "auto-safe"
    };

    private static string VideoSynchronization(VideoSynchronizationMode mode) => mode switch
    {
        VideoSynchronizationMode.DisplayResample          => "display-resample",
        VideoSynchronizationMode.DisplayResampleVideoDrop => "display-resample-vdrop",
        VideoSynchronizationMode.DisplayResampleDesync    => "display-resample-desync",
        VideoSynchronizationMode.DisplayVideoDrop         => "display-vdrop",
        VideoSynchronizationMode.DisplayAudioDrop         => "display-adrop",
        VideoSynchronizationMode.DisplayDesync            => "display-desync",
        VideoSynchronizationMode.Desync                   => "desync",
        _                                                 => "audio"
    };

    private static string Scaling(VideoScalingFilter filter) => filter switch
    {
        VideoScalingFilter.Bicubic         => "bicubic",
        VideoScalingFilter.Spline36        => "spline36",
        VideoScalingFilter.Lanczos         => "lanczos",
        VideoScalingFilter.EwaLanczos      => "ewa_lanczos",
        VideoScalingFilter.EwaLanczosSharp => "ewa_lanczossharp",
        VideoScalingFilter.Mitchell        => "mitchell",
        VideoScalingFilter.CatmullRom      => "catmull_rom",
        VideoScalingFilter.Oversample      => "oversample",
        _                                  => "bilinear"
    };

    private static string DitherDepth(DitherTargetDepth depth) => depth switch
    {
        DitherTargetDepth.Disabled => "no",
        DitherTargetDepth.EightBit => "8",
        DitherTargetDepth.TenBit   => "10",
        _                          => "auto"
    };

    private static string TemporalScaling(TemporalScalingFilter filter) => filter switch
    {
        TemporalScalingFilter.Bicubic    => "bicubic",
        TemporalScalingFilter.Spline16   => "spline16",
        TemporalScalingFilter.Mitchell   => "mitchell",
        TemporalScalingFilter.Oversample => "oversample",
        _                                => "linear"
    };

    private static string AudioOutput(AudioOutputDriver driver) => driver switch
    {
        AudioOutputDriver.Wasapi     => "wasapi",
        AudioOutputDriver.PipeWire   => "pipewire",
        AudioOutputDriver.PulseAudio => "pulse",
        AudioOutputDriver.CoreAudio  => "coreaudio",
        AudioOutputDriver.OpenAl     => "openal",
        AudioOutputDriver.Sdl        => "sdl",
        _                            => "auto"
    };

    private static string ExternalMatch(ExternalFileMatchMode mode) => mode switch
    {
        ExternalFileMatchMode.Disabled => "no",
        ExternalFileMatchMode.Exact    => "exact",
        ExternalFileMatchMode.All      => "all",
        _                              => "fuzzy"
    };

    private static string SubtitleBlend(SubtitleBlendMode mode) => mode switch
    {
        SubtitleBlendMode.Always => "yes",
        SubtitleBlendMode.Video  => "video",
        _                        => "no"
    };

    private static string ScreenshotFormat(ScreenshotImageFormat format) => format switch
    {
        ScreenshotImageFormat.Jpeg   => "jpg",
        ScreenshotImageFormat.WebP   => "webp",
        ScreenshotImageFormat.JpegXl => "jxl",
        _                            => "png"
    };

    private static string ToneMapping(ToneMappingAlgorithm algorithm) => algorithm switch
    {
        ToneMappingAlgorithm.Clip     => "clip",
        ToneMappingAlgorithm.Mobius   => "mobius",
        ToneMappingAlgorithm.Reinhard => "reinhard",
        ToneMappingAlgorithm.Hable    => "hable",
        ToneMappingAlgorithm.Bt2390   => "bt.2390",
        ToneMappingAlgorithm.Gamma    => "gamma",
        ToneMappingAlgorithm.Linear   => "linear",
        ToneMappingAlgorithm.Spline   => "spline",
        ToneMappingAlgorithm.Bt2446A  => "bt.2446a",
        _                             => "auto"
    };
}
