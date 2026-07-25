using Reikou.Models.Enums;

namespace Reikou.Models;

public sealed record MediaTrackInfo(
    long           Id,
    MediaTrackType Type,
    string?        Language,
    string?        Title,
    string?        Codec,
    bool           IsSelected,
    bool           IsDefault,
    bool           IsForced,
    bool           IsExternal);
