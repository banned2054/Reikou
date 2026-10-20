using System;

namespace Reikou.Services.Danmaku;

/// <summary>
///     弹幕关联使用的路径比较策略：Windows 路径大小写不敏感，
///     其他平台保持大小写敏感。键统一先经过 <see cref="Path.GetFullPath(string)" />。
/// </summary>
internal static class DanmakuPaths
{
    public static StringComparer Comparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static bool AreEqual(string left, string right) => Comparer.Equals(left, right);
}
