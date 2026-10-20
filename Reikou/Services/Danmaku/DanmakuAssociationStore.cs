using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Reikou.Services.Danmaku;

/// <summary>视频与 XML 弹幕的关联条目；DelaySeconds 为 null 表示继承全局偏移。</summary>
public sealed record DanmakuAssociationEntry(string XmlPath, double? DelaySeconds = null);

internal sealed record DanmakuAssociationDocument
{
    public int Version { get; init; } = 1;

    public Dictionary<string, DanmakuAssociationEntry> Entries { get; init; } = [];
}

/// <summary>
///     本地视频与 XML 弹幕关联的持久化存储：键为视频完整路径（平台大小写策略见
///     <see cref="DanmakuPaths" />），临时文件 + 原子替换并保留 <c>.bak</c>，主文件损坏时
///     回退备份。条目变更后自动单飞落盘；自动保存以 revision 追踪，快照之后到达的
///     变更不会被跳过，循环保存直到追平最新 revision。
/// </summary>
public sealed class DanmakuAssociationStore
{
    private const int CurrentVersion = 1;

    private readonly string _backupPath;

    private readonly Dictionary<string, DanmakuAssociationEntry> _entries;

    private readonly Lock          _gate     = new();
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    private bool _autoSaveQueued;
    private long _dirtyRevision;
    private long _persistedRevision;
    private bool _recoveredBackup;

    public DanmakuAssociationStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        FilePath    = Path.GetFullPath(filePath);
        _backupPath = $"{FilePath}.bak";
        if (!TryLoadEntries(FilePath, out _entries)) _recoveredBackup = TryLoadEntries(_backupPath, out _entries);
    }

    public string FilePath { get; }

    /// <summary>按视频完整路径查询关联；不存在时返回 null。</summary>
    public DanmakuAssociationEntry? GetEntry(string videoPath)
    {
        var key = NormalizeVideoKey(videoPath);

        lock (_gate)
        {
            return _entries.GetValueOrDefault(key);
        }
    }

    /// <summary>
    ///     写入或替换关联。delaySeconds 为 null 时保留已有关联的偏移（新条目则继承全局），
    ///     显式传值（含 0）才覆盖。
    /// </summary>
    public void SetEntry(string videoPath, string xmlPath, double? delaySeconds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(xmlPath);

        var key      = NormalizeVideoKey(videoPath);
        var fullPath = Path.GetFullPath(xmlPath);
        var delay    = delaySeconds.HasValue ? (double?)NormalizeDelay(delaySeconds.Value) : null;

        lock (_gate)
        {
            _entries.TryGetValue(key, out var existing);
            if (existing is not null && DanmakuPaths.AreEqual(existing.XmlPath, fullPath))
            {
                delay ??= existing.DelaySeconds;
                if (Nullable.Equals(existing.DelaySeconds, delay)) return;

                _entries[key] = existing with { DelaySeconds = delay };
            }
            else
            {
                // 换 XML 时保留既有偏移；新条目 delay 为 null 表示继承全局。
                _entries[key] = new DanmakuAssociationEntry(fullPath, delay ?? existing?.DelaySeconds);
            }

            MarkDirty();
        }
    }

    /// <summary>仅更新已有关联的偏移（传 null 恢复继承全局）；条目不存在时返回 false。</summary>
    public bool TryUpdateDelay(string videoPath, double? delaySeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);

        var key   = NormalizeVideoKey(videoPath);
        var delay = delaySeconds.HasValue ? (double?)NormalizeDelay(delaySeconds.Value) : null;

        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out var existing)) return false;

            if (Nullable.Equals(existing.DelaySeconds, delay)) return true;

            _entries[key] = existing with { DelaySeconds = delay };
            MarkDirty();
            return true;
        }
    }

    /// <summary>移除关联；条目不存在时返回 false。</summary>
    public bool RemoveEntry(string videoPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);

        var key = NormalizeVideoKey(videoPath);

        lock (_gate)
        {
            if (!_entries.Remove(key)) return false;

            MarkDirty();
            return true;
        }
    }

    /// <summary>立即落盘：写完临时文件并 flush 后原子替换，原主文件轮转为 <c>.bak</c>。</summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporaryPath = null;

        try
        {
            DanmakuAssociationDocument document;
            lock (_gate)
            {
                document = new DanmakuAssociationDocument
                {
                    Version = CurrentVersion,
                    Entries = new Dictionary<string, DanmakuAssociationEntry>(_entries, DanmakuPaths.Comparer)
                };
            }

            var directory = Path.GetDirectoryName(FilePath)
                         ?? throw new InvalidOperationException("Association file path has no parent directory.");
            Directory.CreateDirectory(directory);

            temporaryPath = Path.Combine(directory, $".{Path.GetFileName(FilePath)}.{Guid.NewGuid():N}.tmp");
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                                                     FileShare.None, 4096,
                                                     FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer
                     .SerializeAsync(stream, document, DanmakuAssociationJsonContext.Default.DanmakuAssociationDocument,
                                     cancellationToken)
                     .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }

            if (_recoveredBackup)
                File.Move(temporaryPath, FilePath, true);
            else
                ReplaceFile(temporaryPath, FilePath, _backupPath);
            _recoveredBackup = false;
            temporaryPath    = null;
        }
        finally
        {
            if (temporaryPath != null) TryDelete(temporaryPath);

            _saveGate.Release();
        }
    }

    // 调用方已持有 _gate。
    private void MarkDirty()
    {
        _dirtyRevision++;
        if (_autoSaveQueued) return;

        _autoSaveQueued = true;
        _               = Task.Run(RunAutoSaveAsync);
    }

    private async Task RunAutoSaveAsync()
    {
        var failed = false;
        try
        {
            while (true)
            {
                long target;
                lock (_gate)
                {
                    target = _dirtyRevision;
                    if (target == _persistedRevision) break;
                }

                await SaveAsync().ConfigureAwait(false);

                lock (_gate)
                {
                    if (_persistedRevision < target) _persistedRevision = target;
                }
            }
        }
        catch (Exception exception) when (exception is IOException
                                                    or UnauthorizedAccessException
                                                    or NotSupportedException)
        {
            failed = true;
            Debug.WriteLine($"Failed to auto-save danmaku associations: {exception}");
        }
        finally
        {
            lock (_gate)
            {
                _autoSaveQueued = false;
                // 保存期间又有变更时继续追平；失败时停止，留给下一次变更或显式 SaveAsync 重试。
                if (!failed && _dirtyRevision != _persistedRevision) MarkDirty();
            }
        }
    }

    private static string NormalizeVideoKey(string videoPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        return Path.GetFullPath(videoPath);
    }

    private static double NormalizeDelay(double delaySeconds) =>
        double.IsFinite(delaySeconds)
            ? Math.Clamp(delaySeconds, -DanmakuProcessingOptions.MaxDelaySeconds,
                         DanmakuProcessingOptions.MaxDelaySeconds)
            : 0;

    private static bool TryLoadEntries(string path, out Dictionary<string, DanmakuAssociationEntry> entries)
    {
        entries = new Dictionary<string, DanmakuAssociationEntry>(DanmakuPaths.Comparer);
        if (!File.Exists(path)) return false;

        try
        {
            using var stream = File.OpenRead(path);
            var document =
                JsonSerializer.Deserialize(stream, DanmakuAssociationJsonContext.Default.DanmakuAssociationDocument);
            if (document?.Entries == null || document.Version != CurrentVersion) return false;

            foreach (var (videoPath, entry) in document.Entries)
            {
                // 单条损坏只跳过该条，不让整个文件回退备份。
                if (entry is not { } validEntry               ||
                    !TryNormalizePath(videoPath, out var key) ||
                    !TryNormalizePath(validEntry.XmlPath, out var xmlPath))
                    continue;

                entries[key] =
                    new DanmakuAssociationEntry(xmlPath,
                                                validEntry.DelaySeconds.HasValue
                                                    ? NormalizeDelay(validEntry.DelaySeconds.Value)
                                                    : null);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Debug.WriteLine($"Failed to load danmaku associations from '{path}': {exception}");
            return false;
        }
    }

    private static bool TryNormalizePath(string? path, out string fullPath)
    {
        fullPath = "";
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            fullPath = Path.GetFullPath(path);
            return !string.IsNullOrWhiteSpace(fullPath);
        }
        catch (Exception exception) when (exception is ArgumentException
                                                    or PathTooLongException
                                                    or NotSupportedException)
        {
            fullPath = "";
            return false;
        }
    }

    private static void ReplaceFile(string temporaryPath, string targetPath, string backupPath)
    {
        if (!File.Exists(targetPath))
        {
            File.Move(temporaryPath, targetPath);
            return;
        }

        try
        {
            File.Replace(temporaryPath, targetPath, backupPath, true);
        }
        catch (PlatformNotSupportedException)
        {
            File.Copy(targetPath, backupPath, true);
            File.Move(temporaryPath, targetPath, true);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException exception)
        {
            Debug.WriteLine($"Failed to remove temporary association file: {exception}");
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(DanmakuAssociationDocument))]
internal partial class DanmakuAssociationJsonContext : JsonSerializerContext;
