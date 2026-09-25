namespace Loupedeck.NowPlayingPlugin.Tests.Providers
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Loupedeck.NowPlayingPlugin.Models;
    using Loupedeck.NowPlayingPlugin.Providers;
    using Loupedeck.NowPlayingPlugin.Tests.Fakes;

    using Xunit;

    public class NowPlayingCoordinatorTests
    {
        [Fact]
        public void Current_DefaultsToInitializing()
        {
            using var coordinator = new NowPlayingCoordinator(new FakeNowPlayingProvider());

            Assert.Equal(NowPlayingStatus.Initializing, coordinator.Current.Status);
        }

        [Fact]
        public async Task StartAsync_DelegatesToProvider()
        {
            var provider = new FakeNowPlayingProvider();
            using var coordinator = new NowPlayingCoordinator(provider);

            await coordinator.StartAsync();

            Assert.Equal(1, provider.StartCount);
            Assert.False(provider.LastStartCancellationToken.IsCancellationRequested);
        }

        [Fact]
        public async Task StopAsync_CancelsTokenPassedToProviderAndDelegatesStop()
        {
            var provider = new FakeNowPlayingProvider();
            using var coordinator = new NowPlayingCoordinator(provider);
            await coordinator.StartAsync();
            var token = provider.LastStartCancellationToken;

            await coordinator.StopAsync();

            Assert.True(token.IsCancellationRequested);
            Assert.Equal(1, provider.StopCount);
        }

        [Fact]
        public void SnapshotChanged_ForwardsProviderSnapshotsAndUpdatesCurrent()
        {
            var provider = new FakeNowPlayingProvider();
            using var coordinator = new NowPlayingCoordinator(provider);
            var received = new List<NowPlayingSnapshot>();
            coordinator.SnapshotChanged += received.Add;

            var idle = NowPlayingSnapshot.Idle();
            provider.Emit(idle);

            Assert.Single(received);
            Assert.Same(idle, received[0]);
            Assert.Same(idle, coordinator.Current);
        }

        [Fact]
        public void SnapshotChanged_DeduplicatesSnapshotsWithTheSameChangeKey()
        {
            var provider = new FakeNowPlayingProvider();
            using var coordinator = new NowPlayingCoordinator(provider);
            var received = new List<NowPlayingSnapshot>();
            coordinator.SnapshotChanged += received.Add;

            provider.Emit(NowPlayingSnapshot.Idle());
            provider.Emit(NowPlayingSnapshot.Idle());

            // Both instances have ChangeKey "Idle"; the second is a no-op.
            Assert.Single(received);
        }

        [Fact]
        public void SnapshotChanged_PublishesWhenChangeKeyDiffers()
        {
            var provider = new FakeNowPlayingProvider();
            using var coordinator = new NowPlayingCoordinator(provider);
            var received = new List<NowPlayingSnapshot>();
            coordinator.SnapshotChanged += received.Add;

            provider.Emit(NowPlayingSnapshot.Idle());
            provider.Emit(NowPlayingSnapshot.Unavailable("oops"));

            Assert.Equal(2, received.Count);
        }

        [Fact]
        public void Emit_WithNullSnapshot_IsIgnored()
        {
            var provider = new FakeNowPlayingProvider();
            using var coordinator = new NowPlayingCoordinator(provider);
            var received = new List<NowPlayingSnapshot>();
            coordinator.SnapshotChanged += received.Add;

            provider.Emit(null);

            Assert.Empty(received);
            Assert.Equal(NowPlayingStatus.Initializing, coordinator.Current.Status);
        }

        [Fact]
        public void Dispose_UnsubscribesFromProviderAndDisposesIt()
        {
            var provider = new FakeNowPlayingProvider();
            var coordinator = new NowPlayingCoordinator(provider);

            coordinator.Dispose();
            provider.Emit(NowPlayingSnapshot.Idle());

            Assert.Equal(1, provider.DisposeCount);
            // Current must not change after disposal, even if the (misbehaving)
            // provider still raises an event post-unsubscribe race.
            Assert.Equal(NowPlayingStatus.Initializing, coordinator.Current.Status);
        }

        [Fact]
        public void Constructor_ThrowsForNullProvider()
        {
            Assert.Throws<System.ArgumentNullException>(() => new NowPlayingCoordinator(null));
        }
    }
}
