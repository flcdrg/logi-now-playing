namespace Loupedeck.NowPlayingPlugin.Tests.Providers.MacOS
{
    using System;
    using System.IO;

    using Loupedeck.NowPlayingPlugin.Providers.MacOS;

    using Xunit;

    public class MediaRemoteAdapterPathsTests : IDisposable
    {
        private readonly String _root;

        public MediaRemoteAdapterPathsTests()
        {
            this._root = Path.Combine(Path.GetTempPath(), "NowPlayingPluginTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this._root);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(this._root, recursive: true);
            }
            catch (IOException)
            {
                // Best effort cleanup; leftover temp directories are harmless.
            }
        }

        // Mirrors the on-disk layout produced by the CopyPackage build target:
        // "<pluginRoot>/bin/NowPlayingPlugin.dll" and
        // "<pluginRoot>/mac/MediaRemoteAdapter/...".
        private String CreateLayout(Boolean includeTestClient = true)
        {
            var binDir = Path.Combine(this._root, "bin");
            var helperDir = Path.Combine(this._root, "mac", "MediaRemoteAdapter");
            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(Path.Combine(helperDir, "MediaRemoteAdapter.framework"));
            File.WriteAllText(Path.Combine(helperDir, "mediaremote-adapter.pl"), "# fake");
            if (includeTestClient)
            {
                File.WriteAllText(Path.Combine(helperDir, "MediaRemoteAdapterTestClient"), String.Empty);
            }

            var assemblyPath = Path.Combine(binDir, "NowPlayingPlugin.dll");
            File.WriteAllText(assemblyPath, String.Empty);
            return assemblyPath;
        }

        [Fact]
        public void TryResolve_WithValidLayout_ResolvesAllPaths()
        {
            var assemblyPath = this.CreateLayout();

            var resolved = MediaRemoteAdapterPaths.TryResolve(assemblyPath, out var paths, out var error);

            Assert.True(resolved);
            Assert.Null(error);
            Assert.EndsWith("mediaremote-adapter.pl", paths.ScriptPath);
            Assert.EndsWith("MediaRemoteAdapter.framework", paths.FrameworkPath);
            Assert.NotNull(paths.TestClientPath);
            Assert.True(File.Exists(paths.ScriptPath));
            Assert.True(Directory.Exists(paths.FrameworkPath));
        }

        [Fact]
        public void TryResolve_WithoutTestClient_StillResolvesButTestClientPathIsNull()
        {
            var assemblyPath = this.CreateLayout(includeTestClient: false);

            var resolved = MediaRemoteAdapterPaths.TryResolve(assemblyPath, out var paths, out var error);

            Assert.True(resolved);
            Assert.Null(error);
            Assert.Null(paths.TestClientPath);
        }

        [Fact]
        public void TryResolve_WithNullPath_Fails()
        {
            var resolved = MediaRemoteAdapterPaths.TryResolve(null, out var paths, out var error);

            Assert.False(resolved);
            Assert.Null(paths);
            Assert.NotNull(error);
        }

        [Fact]
        public void TryResolve_WithEmptyPath_Fails()
        {
            var resolved = MediaRemoteAdapterPaths.TryResolve(String.Empty, out var paths, out var error);

            Assert.False(resolved);
            Assert.Null(paths);
        }

        [Fact]
        public void TryResolve_WhenScriptMissing_Fails()
        {
            var binDir = Path.Combine(this._root, "bin");
            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(Path.Combine(this._root, "mac", "MediaRemoteAdapter", "MediaRemoteAdapter.framework"));
            var assemblyPath = Path.Combine(binDir, "NowPlayingPlugin.dll");
            File.WriteAllText(assemblyPath, String.Empty);

            var resolved = MediaRemoteAdapterPaths.TryResolve(assemblyPath, out var paths, out var error);

            Assert.False(resolved);
            Assert.Null(paths);
            Assert.Contains("script not found", error);
        }

        [Fact]
        public void TryResolve_WhenFrameworkMissing_Fails()
        {
            var binDir = Path.Combine(this._root, "bin");
            var helperDir = Path.Combine(this._root, "mac", "MediaRemoteAdapter");
            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(helperDir);
            File.WriteAllText(Path.Combine(helperDir, "mediaremote-adapter.pl"), "# fake");
            var assemblyPath = Path.Combine(binDir, "NowPlayingPlugin.dll");
            File.WriteAllText(assemblyPath, String.Empty);

            var resolved = MediaRemoteAdapterPaths.TryResolve(assemblyPath, out var paths, out var error);

            Assert.False(resolved);
            Assert.Contains("framework not found", error);
        }
    }
}
