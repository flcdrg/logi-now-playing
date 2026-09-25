namespace Loupedeck.NowPlayingPlugin.Rendering
{
    using System;

    // Renders now-playing artwork onto a Logi action button image, filling the
    // button and center-cropping any excess (a "cover" fit), using the Logi
    // Actions SDK's own BitmapBuilder/BitmapImage APIs rather than a separate
    // imaging library.
    public static class NowPlayingImageRenderer
    {
        // A neutral background used while artwork is unavailable (e.g. loading,
        // or the current media item has no artwork), so the button never shows a
        // jarring blank/transparent image.
        private static readonly BitmapColor PlaceholderBackground = new BitmapColor(32, 32, 32);

        // Builds a button image for the given artwork bytes and target size.
        // Returns a solid placeholder background when artworkData is null or
        // cannot be decoded, so callers do not need to special-case "no artwork".
        public static BitmapImage Render(Byte[] artworkData, PluginImageSize imageSize)
        {
            using var builder = new BitmapBuilder(imageSize);
            builder.Clear(PlaceholderBackground);

            if (artworkData != null && BitmapImage.TryCreateFromArray(artworkData, out var artwork))
            {
                using (artwork)
                {
                    DrawCoverCropped(builder, artwork);
                }
            }

            return builder.ToImage();
        }

        // Draws `source` onto `builder` scaled uniformly so it fully covers the
        // canvas, center-cropping any overflow in the larger dimension. Content
        // drawn outside the canvas bounds is clipped by the underlying surface.
        private static void DrawCoverCropped(BitmapBuilder builder, BitmapImage source)
        {
            if (source.Width <= 0 || source.Height <= 0)
            {
                return;
            }

            var canvasWidth = builder.Width;
            var canvasHeight = builder.Height;

            var scale = Math.Max(
                (Double)canvasWidth / source.Width,
                (Double)canvasHeight / source.Height);

            var drawnWidth = (Int32)Math.Round(source.Width * scale);
            var drawnHeight = (Int32)Math.Round(source.Height * scale);

            var x = (canvasWidth - drawnWidth) / 2;
            var y = (canvasHeight - drawnHeight) / 2;

            builder.DrawImage(source, x, y, drawnWidth, drawnHeight, BitmapRotation.None);
        }
    }
}
