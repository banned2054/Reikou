namespace TestMpv.Enums;

public enum MpvStatus
{
    Success             = 0,
    EventQueueFull      = -1,
    NoMemory            = -2,
    Uninitialized       = -3,
    InvalidParameter    = -4,
    OptionNotFound      = -5,
    OptionFormat        = -6,
    OptionError         = -7,
    PropertyNotFound    = -8,
    PropertyFormat      = -9,
    PropertyUnavailable = -10,
    PropertyError       = -11,
    Command             = -12,
    LoadingFailed       = -13,
    AoInitFailed        = -14,
    VoInitFailed        = -15,
    NothingToPlay       = -16,
    UnknownFormat       = -17,
    Unsupported         = -18,
    NotImplemented      = -19,
    Generic             = -20
}
