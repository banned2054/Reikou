using Banned.Avalonia.Danmaku.Models.Playback;
using Banned.Avalonia.Danmaku.Services.Playback;
using Banned.Danmaku.Models;
using Banned.Danmaku.Utils.Parsing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Reikou.Services;

internal sealed class XmlDanmakuSourceProvider : IDanmakuSourceProvider
{
    private readonly Lock                       _gate = new();
    private readonly Dictionary<string, string> _sourcePaths;

    private string? _pendingSourcePath;

    public XmlDanmakuSourceProvider() : this([], pendingSourcePath : null)
    {
    }

    private XmlDanmakuSourceProvider(Dictionary<string, string> sourcePaths, string? pendingSourcePath)
    {
        _sourcePaths       = sourcePaths;
        _pendingSourcePath = pendingSourcePath;
    }

    public XmlDanmakuSourceProvider WithSource(PlaybackMediaId? mediaId, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        lock (_gate)
        {
            var sourcePaths = new Dictionary<string, string>(_sourcePaths, StringComparer.OrdinalIgnoreCase);
            var fullPath    = Path.GetFullPath(filePath);
            var pendingPath = _pendingSourcePath;

            if (mediaId == null)
            {
                pendingPath = fullPath;
            }
            else
            {
                sourcePaths[mediaId.Value] = fullPath;
            }

            return new XmlDanmakuSourceProvider(sourcePaths, pendingPath);
        }
    }

    public async ValueTask<DanmakuDocument?> LoadAsync(PlaybackMediaId mediaId, CancellationToken cancellationToken)
    {
        string? sourcePath;

        lock (_gate)
        {
            if (!_sourcePaths.TryGetValue(mediaId.Value, out sourcePath) &&
                _pendingSourcePath != null)
            {
                sourcePath                  = _pendingSourcePath;
                _sourcePaths[mediaId.Value] = sourcePath;
                _pendingSourcePath          = null;
            }
        }

        if (sourcePath == null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var document = await Task.Run(() => BilibiliXmlDanmakuParser.ParseFile(sourcePath), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return document;
    }
}
