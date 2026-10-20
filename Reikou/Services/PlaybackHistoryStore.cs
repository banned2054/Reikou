using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Reikou.Services;

public sealed record RecentPlaybackRecord(string Path, DateTimeOffset LastPlayedUtc);

public sealed record PlaybackProgressRecord(string Path, double PositionSeconds, double DurationSeconds);

internal sealed record PlaybackHistoryDocument
{
    public int                          Version  { get; init; } = 1;
    public List<RecentPlaybackRecord>   Recent   { get; init; } = [];
    public List<PlaybackProgressRecord> Progress { get; init; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(PlaybackHistoryDocument))]
internal partial class PlaybackHistoryJsonContext : JsonSerializerContext;

public sealed class PlaybackHistoryStore
{
    private readonly Lock _gate = new();

    private readonly Dictionary<string, PlaybackProgressRecord> _progress = new(PathComparer);
    private readonly Dictionary<string, RecentPlaybackRecord>   _recent   = new(PathComparer);
    private readonly SemaphoreSlim                              _saveGate = new(1, 1);

    private bool _recoveredBackup;

    public PlaybackHistoryStore(string? historyPath = null)
    {
        HistoryPath = Path.GetFullPath(historyPath ??
                                       Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder
                                                                                 .LocalApplicationData), "Reikou",
                                                    "playback-history.json"));
        var document = TryRead(HistoryPath);
        if (document == null)
        {
            document         = TryRead(HistoryPath + ".bak");
            _recoveredBackup = document != null;
        }

        if (document == null) return;
        foreach (var item in document.Recent ?? [])
        {
            if (item == null || !TryNormalizePath(item.Path, out var path)) continue;
            _recent[path] = item with { Path = path };
        }

        foreach (var item in document.Progress ?? [])
        {
            if (item == null || !TryNormalizePath(item.Path, out var path) ||
                !IsResumePosition(item.PositionSeconds, item.DurationSeconds)) continue;
            _progress[path] = item with { Path = path };
        }

        TrimRecent();
    }

    public string HistoryPath { get; }

    public ImmutableArray<RecentPlaybackRecord> RecentRecords
    {
        get
        {
            lock (_gate)
            {
                return [.._recent.Values.OrderByDescending(x => x.LastPlayedUtc)];
            }
        }
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public void RecordOpened(string path)
    {
        path = Path.GetFullPath(path);
        lock (_gate)
        {
            _recent[path] = new RecentPlaybackRecord(path, DateTimeOffset.UtcNow);
            TrimRecent();
        }
    }

    public double? GetResumePosition(string path)
    {
        path = Path.GetFullPath(path);
        lock (_gate)
        {
            return _progress.TryGetValue(path, out var progress) ? progress.PositionSeconds : null;
        }
    }

    public void RecordProgress(
        string path, double positionSeconds, double durationSeconds, bool rememberPosition, bool completed = false)
    {
        path = Path.GetFullPath(path);
        lock (_gate)
        {
            if (!rememberPosition || completed || !IsResumePosition(positionSeconds, durationSeconds))
                _progress.Remove(path);
            else
                _progress[path] = new PlaybackProgressRecord(path, positionSeconds, Math.Max(0, durationSeconds));
        }
    }

    public void RemoveRecent(string path)
    {
        path = Path.GetFullPath(path);
        lock (_gate)
        {
            _recent.Remove(path);
        }
    }

    public void ClearRecent()
    {
        lock (_gate)
        {
            _recent.Clear();
        }
    }

    public void ClearProgress(string path)
    {
        path = Path.GetFullPath(path);
        lock (_gate)
        {
            _progress.Remove(path);
        }
    }

    public void ClearAllProgress()
    {
        lock (_gate)
        {
            _progress.Clear();
        }
    }

    public static bool IsResumePosition(double position, double duration)
    {
        if (!double.IsFinite(position) || !double.IsFinite(duration) || position < 5 || duration < 0) return false;
        return duration <= 0 || position < duration - Math.Min(30, duration * 0.05);
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        string? temporaryPath = null;
        try
        {
            PlaybackHistoryDocument document;
            lock (_gate)
            {
                document = new PlaybackHistoryDocument
                    { Recent = _recent.Values.ToList(), Progress = _progress.Values.ToList() };
            }

            var directory = Path.GetDirectoryName(HistoryPath)!;
            Directory.CreateDirectory(directory);
            temporaryPath = Path.Combine(directory, $".playback-history.{Guid.NewGuid():N}.tmp");
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                                                     FileShare.None,
                                                     4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer
                     .SerializeAsync(stream, document, PlaybackHistoryJsonContext.Default.PlaybackHistoryDocument,
                                     cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }

            if (!File.Exists(HistoryPath) || _recoveredBackup)
                File.Move(temporaryPath, HistoryPath, true);
            else
                try
                {
                    File.Replace(temporaryPath, HistoryPath, HistoryPath + ".bak", true);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(HistoryPath, HistoryPath + ".bak", true);
                    File.Move(temporaryPath, HistoryPath, true);
                }

            _recoveredBackup = false;
            temporaryPath    = null;
        }
        finally
        {
            if (temporaryPath != null)
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

            _saveGate.Release();
        }
    }

    private void TrimRecent()
    {
        foreach (var key in _recent.Values.OrderByDescending(x => x.LastPlayedUtc).Skip(100).Select(x => x.Path)
                                   .ToArray()) _recent.Remove(key);
    }

    private static bool TryNormalizePath(string? path, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            normalized = Path.GetFullPath(path);
            return true;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static PlaybackHistoryDocument? TryRead(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var document =
                JsonSerializer.Deserialize(stream, PlaybackHistoryJsonContext.Default.PlaybackHistoryDocument);
            return document?.Version == 1 ? document : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }
}
