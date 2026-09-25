namespace Loupedeck.NowPlayingPlugin.Tests.Providers.MacOS
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    using Loupedeck.NowPlayingPlugin.Providers.MacOS;

    using Xunit;

    public class ProcessOutputDrainerTests
    {
        [Fact]
        public async Task DrainNonEmptyLinesAsync_ConsumesAllDiagnosticsWithoutWaitingForProcessExit()
        {
            using var reader = new StringReader("first warning\n\n  second warning  \n");
            var diagnostics = new List<String>();

            await ProcessOutputDrainer.DrainNonEmptyLinesAsync(reader, diagnostics.Add);

            Assert.Equal(new[] { "first warning", "second warning" }, diagnostics);
        }

        [Fact]
        public async Task DrainNonEmptyLinesAsync_WithNullReader_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => ProcessOutputDrainer.DrainNonEmptyLinesAsync(null, _ => { }));
        }

        [Fact]
        public async Task DrainNonEmptyLinesAsync_WithNullCallback_ThrowsArgumentNullException()
        {
            using var reader = new StringReader(String.Empty);

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => ProcessOutputDrainer.DrainNonEmptyLinesAsync(reader, null));
        }
    }
}
