using Reikou.Models.Enums;
using Reikou.Models.Structs;
using Reikou.Native;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Reikou.Services;

public static class MediaMetadataReader
{
    private const           double   EventWaitTimeoutSeconds = 0.1;
    private static readonly TimeSpan DefaultTimeout          = TimeSpan.FromSeconds(5);

    public static Task<double?> TryGetDurationAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return TryGetDurationAsync(filePath, DefaultTimeout, cancellationToken);
    }

    public static async Task<double?> TryGetDurationAsync(string            filePath, TimeSpan timeout,
                                                          CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || timeout <= TimeSpan.Zero)
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await Task.Run(() => TryGetDuration(filePath, timeout, cancellationToken), cancellationToken)
                             .WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    private static double? TryGetDuration(string filePath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var duration = TryGetDurationWithMediaInfo(filePath, cancellationToken);
                if (duration.HasValue)
                {
                    return duration;
                }
            }
            catch (DllNotFoundException)
            {
                // Preserve duration loading when the optional native asset is unavailable.
            }
            catch (EntryPointNotFoundException)
            {
                // Preserve duration loading when the native asset is incompatible.
            }
            catch (BadImageFormatException)
            {
                // Preserve duration loading when the native asset architecture is incompatible.
            }
        }

        return TryGetDurationWithMpv(filePath, timeout, cancellationToken);
    }

    private static double? TryGetDurationWithMediaInfo(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
            return null;

        using var handle = LibMediaInfo.Create();
        if (handle.IsInvalid || !LibMediaInfo.Open(handle, filePath))
            return null;

        cancellationToken.ThrowIfCancellationRequested();

        var durationText = LibMediaInfo.GetGeneral(handle, "Duration");
        if (!double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture,
                             out var durationMilliseconds) ||
            !double.IsFinite(durationMilliseconds)         ||
            durationMilliseconds <= 0)
        {
            return null;
        }

        return durationMilliseconds / 1000;
    }

    private static double? TryGetDurationWithMpv(string filePath, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var handle = LibMpv.Create();
        if (handle == nint.Zero)
            return null;

        try
        {
            ConfigureMetadataOnlyInstance(handle);

            if (LibMpv.Initialize(handle) < 0)
                return null;

            if (LibMpv.Command(handle, "loadfile", filePath, "replace") < 0)
                return null;

            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
            {
                var eventPtr = LibMpv.WaitEvent(handle, EventWaitTimeoutSeconds);
                if (eventPtr == nint.Zero)
                    continue;

                var mpvEvent = Marshal.PtrToStructure<MpvEvent>(eventPtr);
                if (mpvEvent.eventId == MpvEventId.FileLoaded)
                    return ReadDuration(handle);

                if (mpvEvent.eventId is MpvEventId.EndFile or MpvEventId.Shutdown)
                    return ReadDuration(handle);
            }

            return ReadDuration(handle);
        }
        finally
        {
            LibMpv.Destroy(handle);
        }
    }

    private static void ConfigureMetadataOnlyInstance(nint handle)
    {
        LibMpv.SetOptionString(handle, "idle", "yes");
        LibMpv.SetOptionString(handle, "pause", "yes");
        LibMpv.SetOptionString(handle, "vid", "no");
        LibMpv.SetOptionString(handle, "aid", "no");
        LibMpv.SetOptionString(handle, "sid", "no");
        LibMpv.SetOptionString(handle, "cache", "no");
        LibMpv.SetOptionString(handle, "vo", "null");
        LibMpv.SetOptionString(handle, "ao", "null");
        LibMpv.SetOptionString(handle, "demuxer-cache-wait", "no");
    }

    private static double? ReadDuration(nint handle)
    {
        var status = LibMpv.GetProperty(handle, "duration", MpvFormat.Double, out double duration);
        if (status < 0 || double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0)
            return null;

        return duration;
    }
}
