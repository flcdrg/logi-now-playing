namespace Loupedeck.NowPlayingPlugin.Providers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Loupedeck.NowPlayingPlugin.Models;

    // Owns the lifetime of a single INowPlayingProvider and fans out deduplicated
    // snapshot changes to any number of subscribers (in practice, the one shared
    // NowPlayingCommand instance). This avoids running one native helper process
    // per configured button.
    public sealed class NowPlayingCoordinator : IDisposable
    {
        public event Action<NowPlayingSnapshot> SnapshotChanged;

        public NowPlayingSnapshot Current { get; private set; } = NowPlayingSnapshot.Initializing();

        private readonly INowPlayingProvider _provider;
        private readonly Object _lock = new Object();
        private CancellationTokenSource _cancellationTokenSource;
        private Boolean _isDisposed;

        public NowPlayingCoordinator(INowPlayingProvider provider)
        {
            this._provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this._provider.SnapshotChanged += this.OnProviderSnapshotChanged;
        }

        public async Task StartAsync()
        {
            CancellationTokenSource cts;
            lock (this._lock)
            {
                if (this._isDisposed)
                {
                    return;
                }

                this._cancellationTokenSource ??= new CancellationTokenSource();
                cts = this._cancellationTokenSource;
            }

            await this._provider.StartAsync(cts.Token).ConfigureAwait(false);
        }

        public async Task StopAsync()
        {
            CancellationTokenSource cts;
            lock (this._lock)
            {
                cts = this._cancellationTokenSource;
                this._cancellationTokenSource = null;
            }

            cts?.Cancel();
            await this._provider.StopAsync().ConfigureAwait(false);
            cts?.Dispose();
        }

        private void OnProviderSnapshotChanged(NowPlayingSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            lock (this._lock)
            {
                if (this._isDisposed)
                {
                    return;
                }

                // Deduplicate defensively even though well-behaved providers should
                // already avoid raising redundant snapshots.
                if (this.Current != null && this.Current.ChangeKey == snapshot.ChangeKey)
                {
                    return;
                }

                this.Current = snapshot;
            }

            this.SnapshotChanged?.Invoke(snapshot);
        }

        public void Dispose()
        {
            lock (this._lock)
            {
                if (this._isDisposed)
                {
                    return;
                }

                this._isDisposed = true;
            }

            this._provider.SnapshotChanged -= this.OnProviderSnapshotChanged;
            this._cancellationTokenSource?.Cancel();
            this._cancellationTokenSource?.Dispose();
            this._provider.Dispose();
        }
    }
}
