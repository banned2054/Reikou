using System;
using System.Collections.Generic;
using System.IO;

namespace Reikou.Utils;

public static class StringUtils
{
    private static readonly string[] VideoExtensions    = [".mp4", ".mkv", ".avi", ".mov", ".flv"];
    private static readonly string[] SubtitleExtensions = [".ass", ".srt", ".vtt", ".sub"];
    private static readonly string[] DanmakuExtensions  = [".xml"];

    public static IReadOnlyList<string> VideoFilePickerPatterns { get; } =
        Array.ConvertAll(VideoExtensions, extension => $"*{extension}");

    public static IReadOnlyList<string> SubtitleFilePickerPatterns { get; } =
        Array.ConvertAll(SubtitleExtensions, extension => $"*{extension}");

    public static bool IsVideoFile(string filePathOrExtension)
    {
        var extension = GetExtension(filePathOrExtension);
        return VideoExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsSubtitleFile(string filePathOrExtension)
    {
        var extension = GetExtension(filePathOrExtension);
        return SubtitleExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsDanmakuFile(string filePathOrExtension)
    {
        var extension = GetExtension(filePathOrExtension);
        return DanmakuExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetExtension(string filePathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(filePathOrExtension))
            return string.Empty;

        return filePathOrExtension.StartsWith('.')
            ? filePathOrExtension
            : Path.GetExtension(filePathOrExtension);
    }
}
