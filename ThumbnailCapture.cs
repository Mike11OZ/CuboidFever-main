using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// Takes what is on screen right now and turns it into a small PNG for the world list.
// Reads the back buffer the same way ScreenshotManager does, then shrinks it (ThumbnailScaler).
public static class ThumbnailCapture
{
    // PNG bytes, or null if the screen could not be read. Never throws: a missing thumbnail is not worth a crash.
    public static byte[] Capture(GraphicsDevice graphics)
    {
        try
        {
            int width = graphics.PresentationParameters.BackBufferWidth;
            int height = graphics.PresentationParameters.BackBufferHeight;
            if (width <= 0 || height <= 0) return null;

            var pixels = new Color[width * height];
            graphics.GetBackBufferData(pixels);

            // Color is four bytes in the order R, G, B, A, so the array can be read as plain bytes
            byte[] rgba = MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray();
            byte[] small = ThumbnailScaler.Downscale(rgba, width, height, ThumbnailScaler.Width, ThumbnailScaler.Height);

            using var texture = new Texture2D(graphics, ThumbnailScaler.Width, ThumbnailScaler.Height);
            texture.SetData(small);

            using var stream = new MemoryStream();
            texture.SaveAsPng(stream, ThumbnailScaler.Width, ThumbnailScaler.Height);
            return stream.ToArray();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[Save] Could not capture a thumbnail: {e.Message}");
            return null;
        }
    }
}
