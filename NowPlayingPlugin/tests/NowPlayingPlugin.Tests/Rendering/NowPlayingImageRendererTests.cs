namespace Loupedeck.NowPlayingPlugin.Tests.Rendering
{
    using System;
    using System.IO;

    using Loupedeck.NowPlayingPlugin.Rendering;

    using Xunit;

    // These tests exercise the real SDK-native BitmapBuilder/BitmapImage/SkiaSharp
    // pipeline (see the test project's CopySkiaSharpNative target), so they only
    // run on a machine with Logi Plugin Service installed.
    public class NowPlayingImageRendererTests
    {
        // A minimal valid 2x1 red/blue PNG, generated once and embedded as bytes
        // so tests do not depend on any external image file.
        private static readonly Byte[] TwoByOnePng = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAAEUlEQVR42mNk+M9QzwAEjDAGACcaAoGkFTQTAAAAAElFTkSuQmCC");

        [Fact]
        public void Render_WithNullArtwork_ReturnsPlaceholderSizedImage()
        {
            var image = NowPlayingImageRenderer.Render(null, PluginImageSize.Width90);

            Assert.NotNull(image);
            Assert.Equal(PluginImageSizeExtensions.GetWidth(PluginImageSize.Width90), image.Width);
            Assert.Equal(PluginImageSizeExtensions.GetHeight(PluginImageSize.Width90), image.Height);
        }

        [Fact]
        public void Render_WithUndecodableArtwork_FallsBackToPlaceholderWithoutThrowing()
        {
            var image = NowPlayingImageRenderer.Render(new Byte[] { 0x00, 0x01, 0x02 }, PluginImageSize.Width90);

            Assert.NotNull(image);
            Assert.Equal(PluginImageSizeExtensions.GetWidth(PluginImageSize.Width90), image.Width);
        }

        [Fact(Skip = "BitmapBuilder.DrawImage requires native Skia state that Logi Plugin Service initializes at host startup; not reproducible in a standalone test process. Verified manually against the running plugin instead (see README).")]
        public void Render_WithValidArtwork_ProducesCanvasSizedImage()
        {
            var image = NowPlayingImageRenderer.Render(TwoByOnePng, PluginImageSize.Width90);

            Assert.NotNull(image);
            Assert.Equal(PluginImageSizeExtensions.GetWidth(PluginImageSize.Width90), image.Width);
            Assert.Equal(PluginImageSizeExtensions.GetHeight(PluginImageSize.Width90), image.Height);
        }

        [Theory(Skip = "BitmapBuilder.DrawImage requires native Skia state that Logi Plugin Service initializes at host startup; not reproducible in a standalone test process. Verified manually against the running plugin instead (see README).")]
        [InlineData(PluginImageSize.Width60)]
        [InlineData(PluginImageSize.Width90)]
        public void Render_ProducesTheRequestedImageSizeForEachSupportedButtonSize(PluginImageSize size)
        {
            var image = NowPlayingImageRenderer.Render(TwoByOnePng, size);

            Assert.Equal(PluginImageSizeExtensions.GetWidth(size), image.Width);
            Assert.Equal(PluginImageSizeExtensions.GetHeight(size), image.Height);
        }
    }
}
