namespace Loupedeck.NowPlayingPlugin.Models
{
    using System;

    // An immutable snapshot of the currently playing media, as reported by a
    // platform-specific INowPlayingProvider. Instances are compared by ChangeKey
    // to detect meaningful updates (including artwork arriving after the initial
    // metadata event).
    public sealed class NowPlayingMedia
    {
        public String Title { get; }

        public String Artist { get; }

        public String Album { get; }

        public Boolean IsPlaying { get; }

        // Raw encoded artwork bytes (e.g. JPEG/PNG), or null if no artwork is
        // currently available for this media item.
        public Byte[] ArtworkData { get; }

        // A key that changes whenever any field that affects rendering changes,
        // including a late-arriving ArtworkData. Used by the coordinator and the
        // command to decide whether to invalidate cached renders and notify Logi
        // Plugin Service that the action image or display name has changed.
        public String ChangeKey { get; }

        public NowPlayingMedia(
            String title,
            String artist,
            String album,
            Boolean isPlaying,
            Byte[] artworkData,
            String changeKey)
        {
            this.Title = title ?? String.Empty;
            this.Artist = artist ?? String.Empty;
            this.Album = album ?? String.Empty;
            this.IsPlaying = isPlaying;
            this.ArtworkData = artworkData;
            this.ChangeKey = changeKey ?? String.Empty;
        }
    }
}
