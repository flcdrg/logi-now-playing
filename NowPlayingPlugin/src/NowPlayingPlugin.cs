namespace Loupedeck.NowPlayingPlugin
{
    using System;
    using System.Threading.Tasks;

    using Loupedeck.NowPlayingPlugin.Providers;
    using Loupedeck.NowPlayingPlugin.Providers.MacOS;

    // This class contains the plugin-level logic of the Loupedeck plugin.
    public class NowPlayingPlugin : Plugin
    {
        // Gets a value indicating whether this is an API-only plugin.
        public override Boolean UsesApplicationApiOnly => true;

        // Gets a value indicating whether this is a Universal plugin or an Application plugin.
        public override Boolean HasNoApplication => true;

        // Shared by every NowPlayingCommand parameter/button so only a single
        // native helper process runs regardless of how many buttons are configured.
        internal NowPlayingCoordinator Coordinator { get; private set; }

        // Initializes a new instance of the plugin class.
        public NowPlayingPlugin()
        {
            // Initialize the plugin log.
            PluginLog.Init(this.Log);

            // Initialize the plugin resources.
            PluginResources.Init(this.Assembly);

            // Constructed here (rather than in Load()) so that Coordinator is
            // never null when NowPlayingCommand.OnLoad() runs, regardless of
            // whether the SDK loads dynamic actions before or after calling
            // Plugin.Load() on this instance.
            var provider = new MacOsNowPlayingProvider(
                () => this.AssemblyFilePath,
                logInfo: PluginLog.Info,
                logWarning: PluginLog.Warning,
                logError: PluginLog.Error);
            this.Coordinator = new NowPlayingCoordinator(provider);
        }

        // This method is called when the plugin is loaded.
        public override void Load()
        {
            // Fire-and-forget: Load() must return promptly, and the coordinator
            // publishes an Initializing snapshot synchronously so buttons have
            // something reasonable to show immediately.
            _ = this.StartCoordinatorAsync();
        }

        // This method is called when the plugin is unloaded.
        public override void Unload()
        {
            // Best-effort synchronous shutdown; Unload() has no async overload in
            // this SDK version.
            try
            {
                this.Coordinator.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, "Error while stopping the Now Playing coordinator during unload.");
            }
            finally
            {
                this.Coordinator.Dispose();
            }
        }

        private async Task StartCoordinatorAsync()
        {
            try
            {
                await this.Coordinator.StartAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "Failed to start the Now Playing coordinator.");
            }
        }
    }
}
