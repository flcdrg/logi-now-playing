namespace Loupedeck.NowPlayingPlugin.Tests.Models
{
    using System;

    using Loupedeck.NowPlayingPlugin.Models;

    using Xunit;

    public class NowPlayingMediaTests
    {
        [Fact]
        public void Constructor_ReplacesNullStringsWithEmpty()
        {
            var media = new NowPlayingMedia(null, null, null, true, null, null);

            Assert.Equal(String.Empty, media.Title);
            Assert.Equal(String.Empty, media.Artist);
            Assert.Equal(String.Empty, media.Album);
            Assert.Equal(String.Empty, media.ChangeKey);
            Assert.Null(media.ArtworkData);
        }

        [Fact]
        public void Constructor_PreservesProvidedValues()
        {
            var artwork = new Byte[] { 1, 2, 3 };

            var media = new NowPlayingMedia("Title", "Artist", "Album", true, artwork, "key");

            Assert.Equal("Title", media.Title);
            Assert.Equal("Artist", media.Artist);
            Assert.Equal("Album", media.Album);
            Assert.True(media.IsPlaying);
            Assert.Same(artwork, media.ArtworkData);
            Assert.Equal("key", media.ChangeKey);
        }
    }
}
