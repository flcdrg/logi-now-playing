namespace Loupedeck.NowPlayingPlugin.Providers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Loupedeck.NowPlayingPlugin.Models;

    // A platform-neutral source of now-playing media information. Implementations
    // (e.g. a macOS provider backed by a native helper process) own their own
    // background work and must marshal every meaningful state change through
    // SnapshotChanged. Consumers own the provider's lifetime via StartAsync and
    // StopAsync and must not assume anything about the calling thread of the
    // SnapshotChanged event.
    public interface INowPlayingProvider : IDisposable
    {
        // Raised whenever the provider has a new snapshot to publish. Implementations
        // should only raise this event when the snapshot is meaningfully different
        // from the previously published one (the coordinator also deduplicates
        // defensively, but avoiding redundant work in the provider reduces noise).
        event Action<NowPlayingSnapshot> SnapshotChanged;

        // Starts the provider. Must be safe to call once per provider instance.
        // The provider should publish an Initializing snapshot synchronously (or
        // very soon after) and then publish further snapshots as they become
        // available.
        Task StartAsync(CancellationToken cancellationToken);

        // Stops any background work and releases resources owned by the provider.
        // Safe to call even if StartAsync was never called or already completed.
        Task StopAsync();
    }
}
