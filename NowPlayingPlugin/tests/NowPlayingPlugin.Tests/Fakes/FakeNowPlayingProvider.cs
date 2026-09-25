namespace Loupedeck.NowPlayingPlugin.Tests.Fakes
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Loupedeck.NowPlayingPlugin.Models;
    using Loupedeck.NowPlayingPlugin.Providers;

    // A controllable INowPlayingProvider for tests: StartAsync/StopAsync just
    // record call counts, and tests raise snapshots directly via Emit.
    internal sealed class FakeNowPlayingProvider : INowPlayingProvider
    {
        public event Action<NowPlayingSnapshot> SnapshotChanged;

        public Int32 StartCount { get; private set; }

        public Int32 StopCount { get; private set; }

        public Int32 DisposeCount { get; private set; }

        public CancellationToken LastStartCancellationToken { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.StartCount++;
            this.LastStartCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            this.StopCount++;
            return Task.CompletedTask;
        }

        public void Emit(NowPlayingSnapshot snapshot) => this.SnapshotChanged?.Invoke(snapshot);

        public void Dispose() => this.DisposeCount++;
    }
}
