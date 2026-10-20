using Banned.Avalonia.Danmaku.Models.Playback;
using Banned.Avalonia.Danmaku.Services.Playback;
using Banned.Danmaku.Models;
using Banned.Danmaku.Utils.Parsing;
using Reikou.Services.Danmaku;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Reikou.Services;

/// <summary>
///     本地 XML 弹幕源 provider：媒体与 XML 关联（内存 → 手动 pending → 持久化 store）、
///     可取消的过期加载，以及在数据层应用 <see cref="DanmakuProcessingOptions" />。
///     实例不可变；WithSource / WithProcessing 返回新实例，重新赋值视图 SourceProvider 即触发重载。
/// </summary>
internal sealed class XmlDanmakuSourceProvider : IDanmakuSourceProvider
{
    private readonly DanmakuAssociationStore? _associations;
    private readonly Lock                     _gate = new();

    private readonly Dictionary<string, string> _sourcePaths;

    private string? _pendingSourcePath;

    public XmlDanmakuSourceProvider() : this(null, null)
    {
    }

    public XmlDanmakuSourceProvider(DanmakuAssociationStore? associations, DanmakuProcessingOptions? processing)
        : this(new Dictionary<string, string>(DanmakuPaths.Comparer), null, associations,
               processing?.Normalize() ?? DanmakuProcessingOptions.Default)
    {
    }

    private XmlDanmakuSourceProvider(Dictionary<string, string> sourcePaths,  string? pendingSourcePath,
                                     DanmakuAssociationStore?   associations, DanmakuProcessingOptions processing)
    {
        _sourcePaths       = sourcePaths;
        _pendingSourcePath = pendingSourcePath;
        _associations      = associations;
        ProcessingOptions  = processing;
    }

    public DanmakuProcessingOptions ProcessingOptions { get; }

    public async ValueTask<DanmakuDocument?> LoadAsync(PlaybackMediaId mediaId, CancellationToken cancellationToken)
    {
        // 取消必须在修改任何关联状态之前生效，避免取消的加载串改 pending 或关联。
        cancellationToken.ThrowIfCancellationRequested();

        var     mediaKey = NormalizeMediaKey(mediaId.Value);
        string? sourcePath;
        double? perMediaDelay = null;

        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_sourcePaths.TryGetValue(mediaKey, out sourcePath))
            {
                // 手动选择优先于历史关联：pending 只绑定到当前这次加载，无论成败都不再供给其他媒体。
                if (_pendingSourcePath != null)
                {
                    var pendingPath = _pendingSourcePath;
                    _pendingSourcePath = null;
                    if (File.Exists(pendingPath))
                    {
                        sourcePath = _sourcePaths[mediaKey] = pendingPath;
                        _associations?.SetEntry(mediaId.Value, pendingPath);
                    }
                }

                // 无手动选择时使用持久化关联；XML 已删除则不绑定，返回 null 让视频继续播放。
                if (sourcePath == null && _associations?.GetEntry(mediaId.Value) is { } entry &&
                    File.Exists(entry.XmlPath))
                    sourcePath = _sourcePaths[mediaKey] = entry.XmlPath;
            }

            if (sourcePath != null) perMediaDelay = _associations?.GetEntry(mediaId.Value)?.DelaySeconds;
        }

        if (sourcePath == null) return null;

        cancellationToken.ThrowIfCancellationRequested();

        var options = perMediaDelay.HasValue
            ? ProcessingOptions with { Delay = TimeSpan.FromSeconds(perMediaDelay.Value) }
            : ProcessingOptions;

        var document = await Task.Run(() =>
        {
            var parsed = BilibiliXmlDanmakuParser.ParseFile(sourcePath);
            return DanmakuDocumentProcessor.Process(parsed.Items, options);
        }, cancellationToken).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        return document;
    }

    /// <summary>
    ///     关联 XML 与媒体。mediaId 为 null 时进入 pending，由下一次 LoadAsync 绑定到当时媒体；
    ///     携带 mediaId 时写入关联并保留已设置的偏移。
    /// </summary>
    public XmlDanmakuSourceProvider WithSource(PlaybackMediaId? mediaId, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        lock (_gate)
        {
            var fullPath = Path.GetFullPath(filePath);

            if (mediaId == null)
                return new XmlDanmakuSourceProvider(CloneSourcePaths(), fullPath, _associations, ProcessingOptions);

            var mediaKey    = NormalizeMediaKey(mediaId.Value);
            var sourcePaths = CloneSourcePaths();
            sourcePaths[mediaKey] = fullPath;
            _associations?.SetEntry(mediaId.Value, fullPath);

            return new XmlDanmakuSourceProvider(sourcePaths, null, _associations, ProcessingOptions);
        }
    }

    /// <summary>替换数据处理配置；返回新实例，原实例不变。</summary>
    public XmlDanmakuSourceProvider WithProcessing(DanmakuProcessingOptions processing)
    {
        ArgumentNullException.ThrowIfNull(processing);

        lock (_gate)
        {
            return new XmlDanmakuSourceProvider(CloneSourcePaths(), _pendingSourcePath, _associations,
                                                processing.Normalize());
        }
    }

    // 调用方已持有 _gate。
    private Dictionary<string, string> CloneSourcePaths() =>
        new(_sourcePaths, DanmakuPaths.Comparer);

    private static string NormalizeMediaKey(string value) => Path.GetFullPath(value);
}
