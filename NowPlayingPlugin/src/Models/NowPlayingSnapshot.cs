namespace Loupedeck.NowPlayingPlugin.Models
{
    using System;

    // A point-in-time snapshot published by an INowPlayingProvider. Media is null
    // unless Status is Active.
    public sealed class NowPlayingSnapshot
    {
        public NowPlayingStatus Status { get; }

        public NowPlayingMedia Media { get; }

        // A human-readable explanation, populated when Status is Unavailable, for
        // logging and for building diagnostic display text.
        public String ErrorMessage { get; }

        private NowPlayingSnapshot(NowPlayingStatus status, NowPlayingMedia media, String errorMessage)
        {
            this.Status = status;
            this.Media = media;
            this.ErrorMessage = errorMessage;
        }

        public static NowPlayingSnapshot Initializing() =>
            new NowPlayingSnapshot(NowPlayingStatus.Initializing, null, null);

        public static NowPlayingSnapshot Unavailable(String errorMessage) =>
            new NowPlayingSnapshot(NowPlayingStatus.Unavailable, null, errorMessage);

        public static NowPlayingSnapshot Idle() =>
            new NowPlayingSnapshot(NowPlayingStatus.Idle, null, null);

        public static NowPlayingSnapshot Active(NowPlayingMedia media) =>
            new NowPlayingSnapshot(NowPlayingStatus.Active, media ?? throw new ArgumentNullException(nameof(media)), null);

        // A stable key used by NowPlayingCoordinator to detect whether a newly
        // published snapshot is meaningfully different from the previous one.
        public String ChangeKey => this.Status switch
        {
            NowPlayingStatus.Active => "Active:" + this.Media.ChangeKey,
            NowPlayingStatus.Unavailable => "Unavailable:" + (this.ErrorMessage ?? String.Empty),
            _ => this.Status.ToString(),
        };
    }
}
