using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Reikou.Models.Enums;
using Reikou.Models.Settings;
using Reikou.Native;
using Reikou.Services;
using Reikou.ViewModels;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Reikou.Tests;

[TestFixture]
public sealed class CoreBehaviorTests
{
    [Test]
    public async Task ManagedCoreBehavior()
    {
        var root = Path.Combine(Path.GetTempPath(), "Reikou.CoreChecks." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path    = Path.Combine(root, "history.json");
            var a       = Path.Combine(root, "空间 a.mkv");
            var b       = Path.Combine(root, "b.mkv");
            var history = new PlaybackHistoryStore(path);
            history.RecordOpened(a);
            history.RecordProgress(a, 120, 1000, true);
            await history.SaveAsync();
            Check(new PlaybackHistoryStore(path).GetResumePosition(a) == 120, "sourcegen history reload");
            history.RecordProgress(a, 200, 1000, true);
            await history.SaveAsync();
            await File.WriteAllTextAsync(path, "broken");
            history = new PlaybackHistoryStore(path);
            Check(history.GetResumePosition(a) == 120, "backup recovery");
            await history.SaveAsync();
            Check(new PlaybackHistoryStore(path).GetResumePosition(a) == 120, "backup recovery preserves valid backup");
            history.ClearRecent();
            Check(history.GetResumePosition(a) == 120 && history.RecentRecords.IsEmpty,
                  "recent independent of progress");
            history.RecordOpened(a);
            history.RecordProgress(a, 975, 1000, true);
            Check(history.GetResumePosition(a) == null && history.RecentRecords.Length == 1,
                  "near end clears progress not recent");
            history.RecordProgress(a, 120, 1000, true);
            history.RecordProgress(a, 120, 1000, false);
            Check(history.GetResumePosition(a) == null, "remember disabled clears progress");
            history.RecordProgress(a, double.NaN, 1000, true);
            Check(history.GetResumePosition(a) == null, "invalid progress rejected");
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => history.SaveAsync()));
            Check(new PlaybackHistoryStore(path).RecentRecords.Length == 1, "concurrent atomic saves");

            var playlist = new PlaylistViewModel();
            playlist.AddFiles([a, b]);
            Check(playlist.GoToNextAfterEnd(a, false)?.FilePath == b && playlist.CurrentIndex == 1,
                  "EOF advances playlist index");
            Check(playlist.GoToNextAfterEnd(b, false) == null && playlist.CurrentIndex == 1,
                  "last item without loop does not reopen");
            playlist.IsLooping = true;
            Check(playlist.GoToNextAfterEnd(a, false) == null && playlist.CurrentIndex == 1,
                  "stale ended path rejected");
            Check(playlist.GoToNextAfterEnd(b, false)?.FilePath == a && playlist.CurrentIndex == 0,
                  "playlist loop wraps");
            Check(playlist.GoToNextAfterEnd(a, true) == null && playlist.CurrentIndex == 0,
                  "native LoopFile takes priority");
            playlist.Clear();
            playlist.AddFile(a);
            Check(playlist.GoToNextAfterEnd(a, false)?.FilePath == a, "single item playlist loop returns itself");
            playlist.IsLooping = false;
            Check(playlist.GoToNextAfterEnd(a, false) == null, "single item no loop returns null");
            Check(new PlaybackSettings().KeepOpen, "default KeepOpen true");
            var options = MpvSettingsMapper.GetOptions(new AppSettings
            {
                Playback = new PlaybackSettings { LoopPlaylist = true, RememberPosition = true }
            }).ToDictionary(x => x.Key, x => x.Value);
            Check(options["loop-playlist"]         == "no" && options["resume-playback"] == "no" &&
                  options["save-position-on-quit"] == "no", "managed playlist/history own native policies");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Test]
    [Explicit("Requires the repository's local libmpv native binary")]
    [Category("Native")]
    public async Task NativePlaybackIntegration()
    {
        var root = Path.Combine(Path.GetTempPath(), "Reikou.NativeChecks." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var library = Path.Combine(AppContext.BaseDirectory, "Libs", "libmpv-2.dll");
            Assert.That(File.Exists(library), Is.True, "libmpv must be copied from the referenced project");
            NativeLibrary.SetDllImportResolver(typeof(LibMpv).Assembly,
                                               (name, _, _) => name == "mpv" ? NativeLibrary.Load(library) : nint.Zero);
            var wav = Path.Combine(root, "short 空间.wav");
            using (var writer = new BinaryWriter(File.Create(wav)))
            {
                const int samples = 8000;
                writer.Write("RIFF"u8);
                writer.Write(36 + samples * 2);
                writer.Write("WAVEfmt "u8);
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(8000);
                writer.Write(16000);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write("data"u8);
                writer.Write(samples * 2);
                writer.Write(new byte[samples * 2]);
            }

            using var service = new MpvService(NullLogger<MpvService>.Instance,
                                               optionOverrides : new Dictionary<string, string>
                                                   { ["vo"] = "null", ["ao"] = "null", ["hwdec"] = "no" });
            using var session = new MpvPlaybackSession(service);
            var       count   = 0;
            session.Ended += (_, e) =>
            {
                if (e.EndReason == MpvEndFileReason.Eof) Interlocked.Increment(ref count);
            };
            var loaded = 0;
            session.Loaded += (_, _) => Interlocked.Increment(ref loaded);
            session.OpenMedia(wav);
            Check(service.LoadMedia(wav, session.MediaRevision) >= 0, "native load argv");
            await Wait(() => Volatile.Read(ref loaded) == 1, "native revision scoped loaded");
            await Wait(() => Volatile.Read(ref count)  == 1, "keep-open eof-reached handled");
            await Task.Delay(150);
            Check(count == 1 && session.State.ToString() == "Ended", "EOF deduplicated");
            service.SetProperty("keep-open", false);
            session.OpenMedia(wav);
            service.LoadMedia(wav, session.MediaRevision);
            await Wait(() => Volatile.Read(ref count) == 2, "EndFile EOF handled");
            await Task.Delay(150);
            Check(count == 2, "EndFile and eof-reached deduplicated");

            var nativeHistory = new PlaybackHistoryStore(Path.Combine(root, "native-history.json"));
            var advances      = 0;
            using var coordinator = new PlaybackCoordinator(session, nativeHistory, action => action(),
                                                            () => new PlaybackSettings(),
                                                            (file, token) =>
                                                            {
                                                                token.ThrowIfCancellationRequested();
                                                                var result =
                                                                    service.LoadMedia(file, session.MediaRevision);
                                                                if (result < 0) throw new Exception("load failed");
                                                                service.SetProperty("pause", false);
                                                                return Task.CompletedTask;
                                                            },
                                                            position =>
                                                            {
                                                                session.BeginSeek(TimeSpan.FromSeconds(position));
                                                                service.Command("seek",
                                                                                    position.ToString(CultureInfo
                                                                                                .InvariantCulture),
                                                                                    "absolute+exact");
                                                            },
                                                            () => service.Command("stop"), _ =>
                                                            {
                                                                Interlocked.Increment(ref advances);
                                                                return null;
                                                            });
            service.SetProperty("keep-open", true);
            await coordinator.OpenAsync(wav);
            await Wait(() => coordinator.Status.State == PlaybackStatusKind.Playing,
                       "coordinator loaded playing status");
            await coordinator.StopAsync();
            await Task.Delay(1200);
            Check(advances == 0 && coordinator.Status.State == PlaybackStatusKind.Stopped,
                  "manual stop does not advance");
            var copyB = Path.Combine(root, "B.wav");
            var copyC = Path.Combine(root, "C.wav");
            File.Copy(wav, copyB);
            File.Copy(wav, copyC);
            await Task.WhenAll(coordinator.OpenAsync(wav), coordinator.OpenAsync(copyB), coordinator.OpenAsync(copyC));
            await Wait(() => session.CurrentPath == copyC && coordinator.Status.State == PlaybackStatusKind.Playing,
                       "rapid A B C last request wins");
            await Wait(() => advances == 1, "only current C EOF advances");
            await coordinator.CloseAsync();
            Check(new PlaybackHistoryStore(nativeHistory.HistoryPath).RecentRecords.Any(x => x.Path == copyC),
                  "close persists recent");

            var longWav = Path.Combine(root, "long.wav");
            using (var writer = new BinaryWriter(File.Create(longWav)))
            {
                const int samples = 8000 * 60;
                writer.Write("RIFF"u8);
                writer.Write(36 + samples * 2);
                writer.Write("WAVEfmt "u8);
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(8000);
                writer.Write(16000);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write("data"u8);
                writer.Write(samples * 2);
                writer.Write(new byte[samples * 2]);
            }

            var policy = new PlaybackSettings();

            PlaybackCoordinator CreateCoordinator(PlaybackHistoryStore records) => new(session, records,
                     action => action(), () => policy,
                     (file, token) =>
                     {
                         token.ThrowIfCancellationRequested();
                         Check(service.LoadMedia(file, session.MediaRevision) >= 0, "load resume sample");
                         service.SetProperty("pause", false);
                         return Task.CompletedTask;
                     },
                     position =>
                     {
                         session.BeginSeek(TimeSpan.FromSeconds(position));
                         service.Command("seek", position.ToString(CultureInfo.InvariantCulture), "absolute+exact");
                     },
                     () => service.Command("stop"), _ =>
                     {
                         Interlocked.Increment(ref advances);
                         return null;
                     });

            using (var playing = CreateCoordinator(nativeHistory))
            {
                await playing.OpenAsync(longWav);
                await Wait(() => session.NativeState.IsSeekable && session.Duration?.TotalSeconds > 59,
                           "resume sample seekable with duration");
                session.BeginSeek(TimeSpan.FromSeconds(10));
                service.Command("seek", "10", "absolute+exact");
                await Wait(() => session.Position.TotalSeconds >= 9.9 && !session.IsSeeking,
                           "actual seek completed before close");
                await playing.CloseAsync();
            }

            var reopened = new PlaybackHistoryStore(nativeHistory.HistoryPath);
            Check(reopened.GetResumePosition(longWav) >= 9.9, "close saves actual progress");
            using (var resumed = CreateCoordinator(reopened))
            {
                await resumed.OpenAsync(longWav);
                await Wait(() => session.Position.TotalSeconds >= 9.9 && !session.IsSeeking,
                           "reopen restores actual native position");
                await resumed.StopAsync();
                var stoppedPosition = reopened.GetResumePosition(longWav);
                await Task.Delay(200);
                await resumed.CloseAsync();
                Check(new PlaybackHistoryStore(reopened.HistoryPath).GetResumePosition(longWav) == stoppedPosition,
                      "stop then close retains progress");
            }

            policy = policy with { RememberPosition = false };
            using (var disabled = CreateCoordinator(new PlaybackHistoryStore(nativeHistory.HistoryPath)))
            {
                await disabled.OpenAsync(longWav);
                await Wait(() => disabled.Status.State == PlaybackStatusKind.Playing,
                           "disabled remember opens normally");
                Check(session.Position.TotalSeconds < 5, "disabled remember does not restore");
                await disabled.CloseAsync();
                Check(new PlaybackHistoryStore(nativeHistory.HistoryPath).GetResumePosition(longWav) == null,
                      "disabled remember clears saved progress");
            }

            policy = new PlaybackSettings { LoopFile = true };
            service.SetProperty("loop-file", "inf");
            using (var loop = CreateCoordinator(new PlaybackHistoryStore(nativeHistory.HistoryPath)))
            {
                var before = advances;
                await loop.OpenAsync(wav);
                await Task.Delay(2500);
                Check(loop.Status.State == PlaybackStatusKind.Playing && advances == before,
                      "native single file looping does not advance managed list");
                await loop.CloseAsync();
            }

            service.SetProperty("loop-file", "no");
            policy = new PlaybackSettings();
            using (var replay = CreateCoordinator(new PlaybackHistoryStore(nativeHistory.HistoryPath)))
            {
                await replay.OpenAsync(longWav);
                await Wait(() => session.NativeState.IsSeekable, "replay sample loaded");
                service.Command("seek", "59.7", "absolute+exact");
                await Wait(() => replay.Status.State == PlaybackStatusKind.Ended, "replay sample reaches EOF");
                service.Command("seek", "10", "absolute+exact");
                service.SetProperty("pause", false);
                await Wait(() => session.Position.TotalSeconds >= 9.9 && session.Position.TotalSeconds < 20 &&
                                 replay.Status.State           == PlaybackStatusKind.Playing,
                           "seek from EOF resumes same revision");
                await replay.CloseAsync();
                Check(new PlaybackHistoryStore(nativeHistory.HistoryPath).GetResumePosition(longWav) >= 9.9,
                      "replay after EOF saves new progress");
            }

            var sequentialList = new PlaylistViewModel();
            sequentialList.AddFiles([wav, copyB]);
            using (var sequential =
                   new PlaybackCoordinator(session, new PlaybackHistoryStore(Path.Combine(root, "sequential.json")),
                                           action => action(), () => policy,
                                           (file, token) =>
                                           {
                                               token.ThrowIfCancellationRequested();
                                               service.LoadMedia(file, session.MediaRevision);
                                               service.SetProperty("pause", false);
                                               return Task.CompletedTask;
                                           },
                                           _ => { }, () => service.Command("stop"),
                                           file => sequentialList.GoToNextAfterEnd(file, false)?.FilePath))
            {
                await sequential.OpenAsync(wav);
                await Wait(() => sequentialList.CurrentIndex == 1 && session.CurrentPath == copyB,
                           "natural EOF loads actual next playlist media");
                await Wait(() => sequential.Status.State == PlaybackStatusKind.Ended,
                           "last media ends without reopening");
                var revision = session.MediaRevision;
                await Task.Delay(200);
                Check(session.MediaRevision == revision, "non-looping last media stays ended");
                await sequential.OpenAsync(Path.Combine(root, "missing.mp4"));
                await Wait(() => sequential.Status.State == PlaybackStatusKind.Failed, "missing media reports failure");
                Check(sequentialList.CurrentIndex == 1, "failure does not advance playlist");
                await sequential.CloseAsync();
            }

            EventHandler<MpvPlaybackStateChangedEventArgs> throwing = (_, _) => throw new Exception("subscriber check");
            service.PlaybackStateChanged += throwing;
            session.OpenMedia(longWav);
            service.LoadMedia(longWav, session.MediaRevision);
            await Wait(() => session.State.ToString() == "Playing", "event loop survives a throwing subscriber");
            service.PlaybackStateChanged -= throwing;
            session.StopCurrentMedia();
            service.Command("stop");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static void Check(bool value, string message) => Assert.That(value, Is.True, message);

    private static async Task Wait(Func<bool> predicate, string message, int timeout = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
        while (!predicate() && DateTime.UtcNow < deadline) await Task.Delay(10);
        Check(predicate(), message);
    }
}
