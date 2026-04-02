using System;
using System.IO;
using System.Linq;

namespace TestMpv.Utils;

public static class StringUtils
{
    private static readonly string[] VideoExtensions = [".mp4", ".mkv", ".avi", ".mov", ".flv"];
    private static readonly string[] SubtitleExtensions = [".ass", ".srt", ".vtt", ".sub"];

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

    private static string GetExtension(string filePathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(filePathOrExtension))
            return string.Empty;

        return filePathOrExtension.StartsWith('.')
            ? filePathOrExtension
            : Path.GetExtension(filePathOrExtension);
    }
}
