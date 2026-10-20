using Reikou.Models.Settings;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reikou.Services.Danmaku;

/// <summary>
///     描述在弹幕数据层（provider 返回文档前）应用的纯数据处理配置。
///     与 <see cref="DanmakuSettings.Density" />（显示区域比例）无关，二者不混算。
/// </summary>
public sealed record DanmakuProcessingOptions
{
    /// <summary>时间偏移的允许范围（±10 分钟）。</summary>
    public const double MaxDelaySeconds = 600;

    /// <summary>关键词数量的上限。</summary>
    public const int MaxBlockedKeywords = 1000;

    /// <summary>每秒弹幕数量上限的允许范围。</summary>
    public const int MaxCountPerSecondLimit = 1000;

    public static DanmakuProcessingOptions Default { get; } = new();

    /// <summary>
    ///     弹幕时间偏移。正值使弹幕延后显示，负值提前；
    ///     偏移后出现时间早于 0 的弹幕会被丢弃。
    /// </summary>
    public TimeSpan Delay { get; init; }

    /// <summary>
    ///     关键词屏蔽列表。子串匹配、<see cref="StringComparison.OrdinalIgnoreCase" />；
    ///     空白项在 <see cref="Normalize" /> 中被丢弃。
    /// </summary>
    public IReadOnlyList<string> BlockedKeywords { get; init; } = [];

    /// <summary>是否显示滚动弹幕（含从右向左、从左向右与高级定位弹幕）。</summary>
    public bool ShowScroll { get; init; } = true;

    /// <summary>是否显示顶部固定弹幕。</summary>
    public bool ShowTop { get; init; } = true;

    /// <summary>是否显示底部固定弹幕。</summary>
    public bool ShowBottom { get; init; } = true;

    /// <summary>
    ///     真正的数量密度限制：0 表示不限制；大于 0 时每个 1 秒窗口内
    ///     只保留时间最早的 N 条弹幕（在类型与关键词过滤之后应用）。
    /// </summary>
    public int MaxCountPerSecond { get; init; }

    /// <summary>
    ///     值相等：关键词按 OrdinalIgnoreCase 逐项比较，
    ///     便于调用方判断配置是否实际变化（避免不必要地重赋 provider 触发重载）。
    /// </summary>
    public bool Equals(DanmakuProcessingOptions? other) =>
        other is not null                            &&
        Delay             == other.Delay             &&
        ShowScroll        == other.ShowScroll        &&
        ShowTop           == other.ShowTop           &&
        ShowBottom        == other.ShowBottom        &&
        MaxCountPerSecond == other.MaxCountPerSecond &&
        BlockedKeywords.SequenceEqual(other.BlockedKeywords, StringComparer.OrdinalIgnoreCase);

    public static DanmakuProcessingOptions FromSettings(DanmakuSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.Normalize();
        return NormalizeCore(new DanmakuProcessingOptions
        {
            Delay             = TimeSpan.FromSeconds(normalized.DelaySeconds),
            BlockedKeywords   = normalized.BlockedKeywords ?? [],
            ShowScroll        = normalized.ShowScroll,
            ShowTop           = normalized.ShowTop,
            ShowBottom        = normalized.ShowBottom,
            MaxCountPerSecond = normalized.MaxCountPerSecond
        });
    }

    /// <summary>将字段收敛到合法范围，保证处理逻辑只接触规范数据。</summary>
    public DanmakuProcessingOptions Normalize() => NormalizeCore(this);

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(Delay);
        hashCode.Add(ShowScroll);
        hashCode.Add(ShowTop);
        hashCode.Add(ShowBottom);
        hashCode.Add(MaxCountPerSecond);
        foreach (var keyword in BlockedKeywords) hashCode.Add(keyword, StringComparer.OrdinalIgnoreCase);

        return hashCode.ToHashCode();
    }

    private static DanmakuProcessingOptions NormalizeCore(DanmakuProcessingOptions options)
    {
        var delaySeconds = ClampFinite(options.Delay.TotalSeconds, -MaxDelaySeconds, MaxDelaySeconds, 0);
        return options with
        {
            Delay = TimeSpan.FromSeconds(delaySeconds),
            BlockedKeywords = NormalizeKeywords(options.BlockedKeywords),
            ShowScroll = options.ShowScroll,
            ShowTop = options.ShowTop,
            ShowBottom = options.ShowBottom,
            MaxCountPerSecond = (int)Math.Round(ClampFinite(options.MaxCountPerSecond, 0, MaxCountPerSecondLimit, 0))
        };
    }

    private static IReadOnlyList<string> NormalizeKeywords(IReadOnlyList<string>? keywords)
    {
        if (keywords is not { Count: > 0 }) return [];

        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>(keywords.Count);
        foreach (var keyword in keywords)
        {
            if (result.Count >= MaxBlockedKeywords) break;

            var trimmed = keyword?.Trim();
            if (string.IsNullOrEmpty(trimmed) || !seen.Add(trimmed)) continue;

            result.Add(trimmed);
        }

        return result;
    }

    private static double ClampFinite(double value, double minimum, double maximum, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;
}
