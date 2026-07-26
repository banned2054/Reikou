using Reikou.Models.Settings;

namespace Reikou.Services;

public sealed record MpvInitializationOptions(AppSettings Settings)
{
    public static MpvInitializationOptions From(AppSettings settings) => new(settings.Normalize());
}
