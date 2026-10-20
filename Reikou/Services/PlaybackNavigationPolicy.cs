namespace Reikou.Services;

public static class PlaybackNavigationPolicy
{
    public static int? NextAfterEnd(int currentIndex, int count, bool loopFile, bool loopPlaylist)
    {
        if (loopFile || count <= 0 || currentIndex < 0 || currentIndex >= count) return null;

        if (currentIndex + 1 < count) return currentIndex + 1;

        return loopPlaylist ? 0 : null;
    }
}
