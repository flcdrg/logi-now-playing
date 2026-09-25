namespace Loupedeck.NowPlayingPlugin.Providers.MacOS
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;

    using Loupedeck.NowPlayingPlugin.Models;

    // Reads system-wide now-playing metadata on macOS by launching the vendored
    // MediaRemoteAdapter helper (see third_party/mediaremote-adapter/NOTICE.md).
    // The helper invokes Apple's private MediaRemote framework via an
    // OS-entitled `/usr/bin/perl` process; this class never links against the
    // private framework directly and only ever communicates with it as an
    // external process over stdout.
    public sealed class MacOsNowPlayingProvider : INowPlayingProvider
    {
        public event Action<NowPlayingSnapshot> SnapshotChanged;

        private const String PerlExecutablePath = "/usr/bin/perl";
        private static readonly TimeSpan SelfTestTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan[] RestartBackoff =
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30),
        };

        private readonly Func<String> _pluginAssemblyFilePathProvider;
        private readonly Action<String> _logInfo;
        private readonly Action<String> _logWarning;
        private readonly Action<Exception, String> _logError;

        private Task _runLoopTask;
        private Process _currentProcess;
        private readonly Object _processLock = new Object();

        // The assembly file path is supplied as a delegate rather than a plain
        // string because Plugin.AssemblyFilePath is not populated yet when the
        // Plugin subclass's constructor runs; it is only safe to read once
        // StartAsync (called from Plugin.Load()) actually executes.
        public MacOsNowPlayingProvider(
            Func<String> pluginAssemblyFilePathProvider,
            Action<String> logInfo = null,
            Action<String> logWarning = null,
            Action<Exception, String> logError = null)
        {
            this._pluginAssemblyFilePathProvider = pluginAssemblyFilePathProvider ?? throw new ArgumentNullException(nameof(pluginAssemblyFilePathProvider));
            this._logInfo = logInfo ?? (_ => { });
            this._logWarning = logWarning ?? (_ => { });
            this._logError = logError ?? ((_, __) => { });
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.SnapshotChanged?.Invoke(NowPlayingSnapshot.Initializing());

            if (!MediaRemoteAdapterPaths.TryResolve(this._pluginAssemblyFilePathProvider(), out var paths, out var resolveError))
            {
                this._logError(null, resolveError);
                this.SnapshotChanged?.Invoke(NowPlayingSnapshot.Unavailable(resolveError));
                return Task.CompletedTask;
            }

            this._runLoopTask = Task.Run(() => this.RunLoopAsync(paths, cancellationToken), cancellationToken);
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            this.KillCurrentProcess();

            var runLoopTask = this._runLoopTask;
            if (runLoopTask != null)
            {
                try
                {
                    await runLoopTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Expected when StartAsync's cancellation token is cancelled.
                }
            }
        }

        private async Task RunLoopAsync(MediaRemoteAdapterPaths paths, CancellationToken cancellationToken)
        {
            if (!await this.RunSelfTestAsync(paths, cancellationToken).ConfigureAwait(false))
            {
                this.SnapshotChanged?.Invoke(
                    NowPlayingSnapshot.Unavailable(
                        "The MediaRemoteAdapter helper failed its self-test. This usually means the current " +
                        "macOS version is not supported, or the private MediaRemote framework has changed."));
                return;
            }

            var attempt = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                var state = new MediaRemoteAdapterState();
                var streamedAnyData = false;

                try
                {
                    streamedAnyData = await this.RunStreamOnceAsync(paths, state, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    this._logError(ex, "MediaRemoteAdapter stream process failed unexpectedly.");
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                // Reset the backoff sequence whenever the previous run produced
                // at least one line of output, since that indicates the helper
                // was actually working before it stopped.
                attempt = streamedAnyData ? 0 : attempt + 1;

                if (attempt >= RestartBackoff.Length)
                {
                    this.SnapshotChanged?.Invoke(
                        NowPlayingSnapshot.Unavailable(
                            "The MediaRemoteAdapter helper process kept exiting immediately after restart and has been given up on."));
                    return;
                }

                var delay = RestartBackoff[Math.Min(attempt, RestartBackoff.Length - 1)];
                this._logWarning($"MediaRemoteAdapter stream ended; restarting in {delay.TotalSeconds:0}s (attempt {attempt + 1}).");

                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task<Boolean> RunSelfTestAsync(MediaRemoteAdapterPaths paths, CancellationToken cancellationToken)
        {
            if (paths.TestClientPath == null)
            {
                // No test client bundled; assume the adapter works and let the
                // stream command itself fail (and trigger restart/backoff) if not.
                return true;
            }

            var startInfo = new ProcessStartInfo(PerlExecutablePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(paths.ScriptPath);
            startInfo.ArgumentList.Add(paths.FrameworkPath);
            startInfo.ArgumentList.Add(paths.TestClientPath);
            startInfo.ArgumentList.Add("test");

            using var process = new Process { StartInfo = startInfo };
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(SelfTestTimeout);

            try
            {
                process.Start();
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
                return process.ExitCode == 0;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Self-test timed out; treat as failed rather than propagating.
                TryKill(process);
                return false;
            }
        }

        private async Task<Boolean> RunStreamOnceAsync(
            MediaRemoteAdapterPaths paths,
            MediaRemoteAdapterState state,
            CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo(PerlExecutablePath)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add(paths.ScriptPath);
            startInfo.ArgumentList.Add(paths.FrameworkPath);
            startInfo.ArgumentList.Add("stream");
            startInfo.ArgumentList.Add("--debounce=250");

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            lock (this._processLock)
            {
                this._currentProcess = process;
            }

            var receivedAnyLine = false;

            try
            {
                process.Start();

                using var registration = cancellationToken.Register(() => this.KillCurrentProcess());

                String line;
                while ((line = await process.StandardOutput.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    receivedAnyLine = true;
                    this.ProcessStreamLine(line, state);
                }

                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

                if (!cancellationToken.IsCancellationRequested)
                {
                    var stderr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                    if (!String.IsNullOrWhiteSpace(stderr))
                    {
                        this._logWarning($"MediaRemoteAdapter stream exited (code {process.ExitCode}): {stderr.Trim()}");
                    }
                }

                return receivedAnyLine;
            }
            finally
            {
                lock (this._processLock)
                {
                    if (ReferenceEquals(this._currentProcess, process))
                    {
                        this._currentProcess = null;
                    }
                }

                process.Dispose();
            }
        }

        private void ProcessStreamLine(String line, MediaRemoteAdapterState state)
        {
            if (String.IsNullOrWhiteSpace(line))
            {
                return;
            }

            JsonObject envelope;
            try
            {
                envelope = JsonNode.Parse(line) as JsonObject;
            }
            catch (JsonException ex)
            {
                this._logWarning($"Ignoring malformed MediaRemoteAdapter stream line: {ex.Message}");
                return;
            }

            if (envelope == null)
            {
                return;
            }

            var isDiff = envelope.TryGetPropertyValue("diff", out var diffNode)
                && diffNode != null
                && diffNode.GetValueKind() == JsonValueKind.True;

            var payload = envelope.TryGetPropertyValue("payload", out var payloadNode)
                ? payloadNode as JsonObject
                : null;

            if (state.TryApply(payload ?? new JsonObject(), isDiff, out var snapshot))
            {
                this.SnapshotChanged?.Invoke(snapshot);
            }
        }

        private void KillCurrentProcess()
        {
            Process process;
            lock (this._processLock)
            {
                process = this._currentProcess;
            }

            if (process == null)
            {
                return;
            }

            TryTerminateGracefully(process);
        }

        private static void TryTerminateGracefully(Process process)
        {
            try
            {
                if (process.HasExited)
                {
                    return;
                }
            }
            catch (InvalidOperationException)
            {
                return;
            }

            if (!UnixSignal.TryTerminate(process))
            {
                TryKill(process);
                return;
            }

            // Give the adapter script a brief window to shut down cleanly after
            // SIGTERM before escalating to a forceful kill.
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                    if (!process.HasExited)
                    {
                        TryKill(process);
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process already disposed.
                }
            });
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception)
            {
                // Best-effort; the process may have exited concurrently.
            }
        }

        public void Dispose()
        {
            this.KillCurrentProcess();
        }
    }
}
