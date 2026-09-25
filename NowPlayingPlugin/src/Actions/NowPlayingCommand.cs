namespace Loupedeck.NowPlayingPlugin.Actions
{
    using System;

    using Loupedeck.NowPlayingPlugin.Models;
    using Loupedeck.NowPlayingPlugin.Providers;
    using Loupedeck.NowPlayingPlugin.Rendering;

    // A single, display-only dynamic command with one parameter per
    // NowPlayingDisplayMode. Pressing the button does nothing in this version;
    // the command exists purely to show what is currently playing.
    //
    // Logi Plugin Service discovers dynamic commands by reflection and requires
    // a public parameterless constructor, so this class cannot take the
    // NowPlayingCoordinator as a constructor argument. Instead it subscribes to
    // the coordinator owned by the parent NowPlayingPlugin from OnLoad(), once
    // the SDK has set the Plugin property.
    public class NowPlayingCommand : PluginDynamicCommand
    {
        private const String ArtworkAndTitleParameter = "ArtworkAndTitle";
        private const String ArtworkOnlyParameter = "ArtworkOnly";
        private const String TitleOnlyParameter = "TitleOnly";

        private NowPlayingCoordinator _coordinator;

        public NowPlayingCommand()
            : base(displayName: "Now Playing", description: "Shows the currently playing media", groupName: "Now Playing")
        {
            this.AddParameter(ArtworkAndTitleParameter, "Artwork and title", "Now Playing");
            this.AddParameter(ArtworkOnlyParameter, "Artwork only", "Now Playing");
            this.AddParameter(TitleOnlyParameter, "Title only", "Now Playing");
        }

        protected override Boolean OnLoad()
        {
            if (this.Plugin is NowPlayingPlugin plugin && plugin.Coordinator != null)
            {
                this._coordinator = plugin.Coordinator;
                this._coordinator.SnapshotChanged += this.OnSnapshotChanged;
            }

            return base.OnLoad();
        }

        // Display-only in this version: button presses are intentionally a no-op.
        protected override void RunCommand(String actionParameter)
        {
        }

        protected override String GetCommandDisplayName(String actionParameter, PluginImageSize imageSize)
        {
            if (actionParameter == ArtworkOnlyParameter)
            {
                // The image alone communicates the state in this mode.
                return String.Empty;
            }

            return DescribeSnapshot(this._coordinator?.Current);
        }

        protected override BitmapImage GetCommandImage(String actionParameter, PluginImageSize imageSize)
        {
            if (actionParameter == TitleOnlyParameter)
            {
                // No custom image in this mode; the SDK falls back to the
                // action's default icon alongside the display name text.
                return null;
            }

            var snapshot = this._coordinator?.Current;
            var artworkData = snapshot?.Status == NowPlayingStatus.Active ? snapshot.Media.ArtworkData : null;
            return NowPlayingImageRenderer.Render(artworkData, imageSize);
        }

        private static String DescribeSnapshot(NowPlayingSnapshot snapshot)
        {
            switch (snapshot?.Status)
            {
                case NowPlayingStatus.Active:
                    var media = snapshot.Media;
                    var title = String.IsNullOrEmpty(media.Title) ? "Unknown title" : media.Title;
                    return String.IsNullOrEmpty(media.Artist)
                        ? title
                        : $"{title}{Environment.NewLine}{media.Artist}";

                case NowPlayingStatus.Idle:
                    return "Nothing playing";

                case NowPlayingStatus.Unavailable:
                    return "Now Playing unavailable";

                case NowPlayingStatus.Initializing:
                default:
                    return "Loading\u2026";
            }
        }

        private void OnSnapshotChanged(NowPlayingSnapshot snapshot)
        {
            // A single media change may affect the image (artwork/idle/unavailable
            // state) and/or the display name (title) depending on the mode, so all
            // three parameters are invalidated together; Logi Plugin Service only
            // re-renders what a given configured button actually shows.
            this.ActionImageChanged(ArtworkAndTitleParameter);
            this.ActionImageChanged(ArtworkOnlyParameter);
            this.ActionImageChanged(TitleOnlyParameter);
        }
    }
}
