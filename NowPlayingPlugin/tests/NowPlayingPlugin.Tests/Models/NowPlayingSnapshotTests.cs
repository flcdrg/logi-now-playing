namespace Loupedeck.NowPlayingPlugin.Tests.Models
{
    using System;

    using Loupedeck.NowPlayingPlugin.Models;

    using Xunit;

    public class NowPlayingSnapshotTests
    {
        [Fact]
        public void Initializing_HasInitializingStatusAndNoMedia()
        {
            var snapshot = NowPlayingSnapshot.Initializing();

            Assert.Equal(NowPlayingStatus.Initializing, snapshot.Status);
            Assert.Null(snapshot.Media);
            Assert.Equal("Initializing", snapshot.ChangeKey);
        }

        [Fact]
        public void Idle_HasIdleStatusAndNoMedia()
        {
            var snapshot = NowPlayingSnapshot.Idle();

            Assert.Equal(NowPlayingStatus.Idle, snapshot.Status);
            Assert.Null(snapshot.Media);
            Assert.Equal("Idle", snapshot.ChangeKey);
        }

        [Fact]
        public void Unavailable_StoresErrorMessageAndIncludesItInChangeKey()
        {
            var snapshot = NowPlayingSnapshot.Unavailable("boom");

            Assert.Equal(NowPlayingStatus.Unavailable, snapshot.Status);
            Assert.Equal("boom", snapshot.ErrorMessage);
            Assert.Equal("Unavailable:boom", snapshot.ChangeKey);
        }

        [Fact]
        public void Unavailable_WithNullMessage_DoesNotThrowAndProducesStableChangeKey()
        {
            var snapshot = NowPlayingSnapshot.Unavailable(null);

            Assert.Equal("Unavailable:", snapshot.ChangeKey);
        }

        [Fact]
        public void Active_RequiresNonNullMedia()
        {
            Assert.Throws<ArgumentNullException>(() => NowPlayingSnapshot.Active(null));
        }

        [Fact]
        public void Active_ChangeKeyIsDerivedFromMediaChangeKey()
        {
            var media = new NowPlayingMedia("Title", "Artist", "Album", true, null, "media-key");

            var snapshot = NowPlayingSnapshot.Active(media);

            Assert.Equal(NowPlayingStatus.Active, snapshot.Status);
            Assert.Same(media, snapshot.Media);
            Assert.Equal("Active:media-key", snapshot.ChangeKey);
        }
    }
}
