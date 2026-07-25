using Reikou.Models.Enums;
using Reikou.Models.Structs;
using Reikou.Native;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Reikou.Services;

public static class MediaMetadataReader
{
    private const double EventWaitTimeoutSeconds = 0.1;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    public static Task<double?> TryGetDurationAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return TryGetDurationAsync(filePath, DefaultTimeout, cancellationToken);
    }

    public static Task<double?> TryGetDurationAsync(
        string filePath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return Task.FromResult<double?>(null);

        return Task.Run(() => TryGetDuration(filePath, timeout, cancellationToken), cancellationToken);
    }

    private static double? TryGetDuration(string filePath, TimeSpan timeout, CancellationToken cancellationToken)
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
