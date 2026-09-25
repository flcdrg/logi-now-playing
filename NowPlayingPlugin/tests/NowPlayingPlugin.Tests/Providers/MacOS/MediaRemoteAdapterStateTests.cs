namespace Loupedeck.NowPlayingPlugin.Tests.Providers.MacOS
{
    using System;
    using System.Text.Json.Nodes;

    using Loupedeck.NowPlayingPlugin.Models;
    using Loupedeck.NowPlayingPlugin.Providers.MacOS;

    using Xunit;

    public class MediaRemoteAdapterStateTests
    {
        [Fact]
        public void TryApply_WithNoBundleIdentifierOrTitle_ProducesIdleSnapshot()
        {
            var state = new MediaRemoteAdapterState();
            var payload = new JsonObject { ["elapsedTime"] = 12.3 };

            var applied = state.TryApply(payload, isDiff: false, out var snapshot);

            Assert.True(applied);
            Assert.Equal(NowPlayingStatus.Idle, snapshot.Status);
        }

        [Fact]
        public void TryApply_WithTitleAndBundleIdentifier_ProducesActiveSnapshotWithMedia()
        {
            var state = new MediaRemoteAdapterState();
            var payload = new JsonObject
            {
                ["bundleIdentifier"] = "org.mozilla.firefox",
                ["title"] = "Eple",
                ["artist"] = "Royksopp",
                ["album"] = "Lunch",
                ["playing"] = true,
            };

            var applied = state.TryApply(payload, isDiff: false, out var snapshot);

            Assert.True(applied);
            Assert.Equal(NowPlayingStatus.Active, snapshot.Status);
            Assert.Equal("Eple", snapshot.Media.Title);
            Assert.Equal("Royksopp", snapshot.Media.Artist);
            Assert.Equal("Lunch", snapshot.Media.Album);
            Assert.True(snapshot.Media.IsPlaying);
            Assert.Null(snapshot.Media.ArtworkData);
        }

        [Fact]
        public void TryApply_DiffUpdate_MergesOnlyChangedKeysIntoPriorFullState()
        {
            var state = new MediaRemoteAdapterState();
            state.TryApply(
                new JsonObject
                {
                    ["bundleIdentifier"] = "org.mozilla.firefox",
                    ["title"] = "Eple",
                    ["artist"] = "Royksopp",
                    ["playing"] = true,
                },
                isDiff: false,
                out _);

            var applied = state.TryApply(
                new JsonObject { ["playing"] = false },
                isDiff: true,
                out var snapshot);

            Assert.True(applied);
            Assert.Equal(NowPlayingStatus.Active, snapshot.Status);
            // Title/artist are preserved from the earlier full snapshot.
            Assert.Equal("Eple", snapshot.Media.Title);
            Assert.Equal("Royksopp", snapshot.Media.Artist);
            Assert.False(snapshot.Media.IsPlaying);
        }

        [Fact]
        public void TryApply_DiffUpdateWithJsonNullValue_RemovesKeyFromState()
        {
            var state = new MediaRemoteAdapterState();
            state.TryApply(
                new JsonObject
                {
                    ["bundleIdentifier"] = "org.mozilla.firefox",
                    ["title"] = "Eple",
                    ["album"] = "Lunch",
                },
                isDiff: false,
                out _);

            var applied = state.TryApply(
                new JsonObject { ["album"] = null },
                isDiff: true,
                out var snapshot);

            Assert.True(applied);
            Assert.Equal(String.Empty, snapshot.Media.Album);
        }

        [Fact]
        public void TryApply_DiffUpdateTouchingOnlyIrrelevantKeys_IsIgnoredAfterFirstPayload()
        {
            var state = new MediaRemoteAdapterState();
            state.TryApply(
                new JsonObject { ["bundleIdentifier"] = "org.mozilla.firefox", ["title"] = "Eple" },
                isDiff: false,
                out _);

            var applied = state.TryApply(
                new JsonObject { ["elapsedTime"] = 42.0, ["timestamp"] = "2026-01-01T00:00:00Z" },
                isDiff: true,
                out var snapshot);

            Assert.False(applied);
            Assert.Null(snapshot);
        }

        [Fact]
        public void TryApply_FirstPayloadIsAlwaysProcessedEvenIfOnlyIrrelevantKeysArePresent()
        {
            var state = new MediaRemoteAdapterState();

            var applied = state.TryApply(
                new JsonObject { ["elapsedTime"] = 1.0 },
                isDiff: true,
                out var snapshot);

            Assert.True(applied);
            Assert.Equal(NowPlayingStatus.Idle, snapshot.Status);
        }

        [Fact]
        public void TryApply_WithNullPayload_TreatsItAsEmptyObject()
        {
            var state = new MediaRemoteAdapterState();

            var applied = state.TryApply(null, isDiff: false, out var snapshot);

            Assert.True(applied);
            Assert.Equal(NowPlayingStatus.Idle, snapshot.Status);
        }

        [Fact]
        public void TryApply_WithMalformedBase64Artwork_IgnoresArtworkWithoutThrowing()
        {
            var state = new MediaRemoteAdapterState();

            var applied = state.TryApply(
                new JsonObject
                {
                    ["bundleIdentifier"] = "org.mozilla.firefox",
                    ["title"] = "Eple",
                    ["artworkData"] = "not-valid-base64!!!",
                },
                isDiff: false,
                out var snapshot);

            Assert.True(applied);
            Assert.Null(snapshot.Media.ArtworkData);
        }

        [Fact]
        public void TryApply_WithOversizedArtwork_DropsArtworkButKeepsMedia()
        {
            var state = new MediaRemoteAdapterState();
            var oversized = new Byte[16 * 1024 * 1024 + 1];
            var base64 = Convert.ToBase64String(oversized);

            var applied = state.TryApply(
                new JsonObject
                {
                    ["bundleIdentifier"] = "org.mozilla.firefox",
                    ["title"] = "Eple",
                    ["artworkData"] = base64,
                },
                isDiff: false,
                out var snapshot);

            Assert.True(applied);
            Assert.Equal(NowPlayingStatus.Active, snapshot.Status);
            Assert.Null(snapshot.Media.ArtworkData);
        }

        [Fact]
        public void TryApply_WithValidArtwork_DecodesBase64Bytes()
        {
            var state = new MediaRemoteAdapterState();
            var artworkBytes = new Byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
            var base64 = Convert.ToBase64String(artworkBytes);

            var applied = state.TryApply(
                new JsonObject
                {
                    ["bundleIdentifier"] = "org.mozilla.firefox",
                    ["title"] = "Eple",
                    ["artworkData"] = base64,
                },
                isDiff: false,
                out var snapshot);

            Assert.True(applied);
            Assert.Equal(artworkBytes, snapshot.Media.ArtworkData);
        }

        [Fact]
        public void TryApply_ArtworkArrivingLateInADiff_ChangesChangeKey()
        {
            var state = new MediaRemoteAdapterState();
            state.TryApply(
                new JsonObject { ["bundleIdentifier"] = "org.mozilla.firefox", ["title"] = "Eple" },
                isDiff: false,
                out var firstSnapshot);

            var artworkBytes = new Byte[] { 1, 2, 3, 4 };
            state.TryApply(
                new JsonObject { ["artworkData"] = Convert.ToBase64String(artworkBytes) },
                isDiff: true,
                out var secondSnapshot);

            Assert.NotEqual(firstSnapshot.ChangeKey, secondSnapshot.ChangeKey);
            Assert.Equal(artworkBytes, secondSnapshot.Media.ArtworkData);
        }

        [Fact]
        public void TryApply_FullReplacementClearsKeysNotPresentInNewPayload()
        {
            var state = new MediaRemoteAdapterState();
            state.TryApply(
                new JsonObject
                {
                    ["bundleIdentifier"] = "org.mozilla.firefox",
                    ["title"] = "Eple",
                    ["artist"] = "Royksopp",
                },
                isDiff: false,
                out _);

            // A subsequent full (non-diff) snapshot replaces the entire state, so
            // "artist" being absent here means it is gone, not merged.
            state.TryApply(
                new JsonObject { ["bundleIdentifier"] = "org.mozilla.firefox", ["title"] = "Other Song" },
                isDiff: false,
                out var snapshot);

            Assert.Equal(String.Empty, snapshot.Media.Artist);
            Assert.Equal("Other Song", snapshot.Media.Title);
        }
    }
}
