namespace Loupedeck.NowPlayingPlugin.Models
{
    // Describes the high-level state of the now-playing data source, independent
    // of the platform-specific provider that produced it.
    public enum NowPlayingStatus
    {
        // The provider has not produced a first result yet (e.g. still starting the
        // native helper process).
        Initializing,

        // The provider could not be started or has stopped working (e.g. the native
        // helper failed its self-test or crashed and could not be restarted).
        Unavailable,

        // The provider is working, but no application currently reports playing or
        // paused media.
        Idle,

        // The provider is working and reports a media item, which may be playing or
        // paused; see NowPlayingMedia.IsPlaying.
        Active,
    }
}
