namespace Loupedeck.NowPlayingPlugin.Providers.MacOS
{
    using System;
    using System.IO;

    // Resolves the on-disk locations of the vendored MediaRemoteAdapter helper
    // relative to the running plugin assembly, and validates that they exist
    // before anything tries to launch them.
    internal sealed class MediaRemoteAdapterPaths
    {
        public String ScriptPath { get; }

        public String FrameworkPath { get; }

        public String TestClientPath { get; }

        private MediaRemoteAdapterPaths(String scriptPath, String frameworkPath, String testClientPath)
        {
            this.ScriptPath = scriptPath;
            this.FrameworkPath = frameworkPath;
            this.TestClientPath = testClientPath;
        }

        // Resolves paths based on the plugin's on-disk assembly file path (Logi
        // Plugin Service loads the plugin assembly in a way that leaves
        // Assembly.Location empty, so callers must pass Plugin.AssemblyFilePath
        // instead). The vendored helper is expected at
        // "<pluginRoot>/mac/MediaRemoteAdapter/" where <pluginRoot> is the parent
        // of the folder containing the assembly (see LoupedeckPackage.yaml's
        // pluginFolderMac and the CopyPackage build target in the .csproj).
        public static Boolean TryResolve(String pluginAssemblyFilePath, out MediaRemoteAdapterPaths paths, out String error)
        {
            paths = null;
            error = null;

            try
            {
                if (String.IsNullOrEmpty(pluginAssemblyFilePath))
                {
                    error = "The plugin's assembly file path is not available.";
                    return false;
                }

                var assemblyDirectory = Path.GetDirectoryName(pluginAssemblyFilePath);
                if (String.IsNullOrEmpty(assemblyDirectory))
                {
                    error = "Could not determine the plugin assembly's directory.";
                    return false;
                }

                var pluginRoot = Directory.GetParent(assemblyDirectory)?.FullName;
                if (String.IsNullOrEmpty(pluginRoot))
                {
                    error = "Could not determine the plugin root directory.";
                    return false;
                }

                var helperDir = Path.Combine(pluginRoot, "mac", "MediaRemoteAdapter");
                var scriptPath = Path.Combine(helperDir, "mediaremote-adapter.pl");
                var frameworkPath = Path.Combine(helperDir, "MediaRemoteAdapter.framework");
                var testClientPath = Path.Combine(helperDir, "MediaRemoteAdapterTestClient");

                if (!File.Exists(scriptPath))
                {
                    error = $"MediaRemoteAdapter script not found at '{scriptPath}'.";
                    return false;
                }

                if (!Directory.Exists(frameworkPath))
                {
                    error = $"MediaRemoteAdapter.framework not found at '{frameworkPath}'.";
                    return false;
                }

                // The test client is optional; its absence only disables the
                // self-test, not streaming.
                paths = new MediaRemoteAdapterPaths(
                    scriptPath,
                    frameworkPath,
                    File.Exists(testClientPath) ? testClientPath : null);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to resolve MediaRemoteAdapter paths: {ex.Message}";
                return false;
            }
        }
    }
}

