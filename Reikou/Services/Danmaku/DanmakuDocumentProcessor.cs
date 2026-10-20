using Banned.Danmaku.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Reikou.Services.Danmaku;

/// <summary>
///     弹幕数据层纯处理管道：类型开关 → 关键词屏蔽 → 时间偏移 → 数量密度限制。
///     输入输出均为 <see cref="Banned.Danmaku.Models" /> 的不可变数据，不依赖 UI，
///     便于脱离 Avalonia 做纯数据验证。
/// </summary>
public static class DanmakuDocumentProcessor
{
    /// <summary>
    ///     按配置处理弹幕集合并生成新文档（文档构造时按出现时间升序排序）。
    /// </summary>
    public static DanmakuDocument Process(IEnumerable<DanmakuItem> items, DanmakuProcessingOptions? options)
    {
        ArgumentNullException.ThrowIfNull(items);

        var effective = (options ?? DanmakuProcessingOptions.Default).Normalize();
        var current   = items;

        if (!effective.ShowScroll || !effective.ShowTop || !effective.ShowBottom)
            current = current.Where(item => PassesModeFilter(item, effective));

        if (effective.BlockedKeywords.Count > 0)
        {
            var keywords = effective.BlockedKeywords;
            current = current.Where(item => !IsBlocked(item.Text, keywords));
        }

        if (effective.Delay != TimeSpan.Zero)
        {
            var delay = effective.Delay;
            current = current
                     .Select(item => new DanmakuItem
                      {
                          Time     = item.Time + delay,
                          Mode     = item.Mode,
                          Text     = item.Text,
                          Color    = item.Color,
                          FontSize = item.FontSize,
                          Duration = item.Duration
                      })
                     .Where(item => item.Time >= TimeSpan.Zero);
        }

        var finalItems = ApplyDensityQuota(current, effective.MaxCountPerSecond);
        return new DanmakuDocument(finalItems);
    }

    private static bool PassesModeFilter(DanmakuItem item, DanmakuProcessingOptions options) => item.Mode switch
    {
        DanmakuMode.ScrollRightToLeft or DanmakuMode.ScrollLeftToRight or DanmakuMode.Advanced => options.ShowScroll,
        DanmakuMode.Top                                                                        => options.ShowTop,
        DanmakuMode.Bottom                                                                     => options.ShowBottom,
        _                                                                                      => true
    };

    private static bool IsBlocked(string text, IReadOnlyList<string> keywords)
    {
        if (string.IsNullOrEmpty(text)) return false;

        // keywords 已由 DanmakuProcessingOptions.Normalize 修剪且非空。
        foreach (var keyword in keywords)
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    /// <summary>
    ///     数量密度限制：先按时间排序，再按 1 秒窗口（<see cref="TimeSpan.TicksPerSecond" /> 取整）
    ///     只保留每个窗口内最早的 <paramref name="maxPerSecond" /> 条；0 表示不限制。
    /// </summary>
    private static List<DanmakuItem> ApplyDensityQuota(IEnumerable<DanmakuItem> items, int maxPerSecond)
    {
        if (maxPerSecond <= 0) return [.. items];

        var result        = new List<DanmakuItem>();
        var currentWindow = long.MinValue;
        var countInWindow = 0;

        foreach (var item in items.OrderBy(item => item.Time))
        {
            var window = item.Time.Ticks / TimeSpan.TicksPerSecond;
            if (window != currentWindow)
            {
                currentWindow = window;
                countInWindow = 0;
            }

            if (countInWindow >= maxPerSecond) continue;

            result.Add(item);
            countInWindow++;
        }

        return result;
    }
}
