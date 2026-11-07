using System;

namespace CuboidFever;

// Shrinks a screenshot to thumbnail size. Works on plain RGBA bytes (4 per pixel, row by row) rather than
// on graphics types, so it can be tested without a graphics device.
public static class ThumbnailScaler
{
    // The thumbnail shape (16:9) and size. Big enough to look good in a list row, small enough that the PNG
    // stays a few tens of kilobytes.
    public const int Width = 320;
    public const int Height = 180;

    // Crops the middle of the source to the output's shape (so a wide or tall window is not stretched), then
    // averages each block of source pixels into one output pixel. The result is fully opaque, because a
    // back buffer's alpha channel is not meaningful.
    public static byte[] Downscale(byte[] rgba, int sourceWidth, int sourceHeight, int outWidth, int outHeight)
    {
        if (rgba == null) throw new ArgumentNullException(nameof(rgba));
        if (sourceWidth <= 0 || sourceHeight <= 0 || outWidth <= 0 || outHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), "Sizes must be positive.");
        if (rgba.Length < (long)sourceWidth * sourceHeight * 4)
            throw new ArgumentException("Not enough pixel data for the given size.", nameof(rgba));

        // The largest area of the source with the output's shape, centered
        long cropWidthLong = Math.Min(sourceWidth, (long)sourceHeight * outWidth / outHeight);
        int cropWidth = (int)Math.Max(1, cropWidthLong);
        int cropHeight = (int)Math.Max(1, Math.Min(sourceHeight, (long)cropWidth * outHeight / outWidth));
        int cropX = (sourceWidth - cropWidth) / 2;
        int cropY = (sourceHeight - cropHeight) / 2;

        var output = new byte[outWidth * outHeight * 4];

        for (int oy = 0; oy < outHeight; oy++)
        {
            int y0 = cropY + (int)((long)oy * cropHeight / outHeight);
            int y1 = cropY + (int)((long)(oy + 1) * cropHeight / outHeight);
            if (y1 <= y0) y1 = y0 + 1; // upscaling: every output pixel still reads at least one source pixel

            for (int ox = 0; ox < outWidth; ox++)
            {
                int x0 = cropX + (int)((long)ox * cropWidth / outWidth);
                int x1 = cropX + (int)((long)(ox + 1) * cropWidth / outWidth);
                if (x1 <= x0) x1 = x0 + 1;

                long r = 0, g = 0, b = 0;
                int count = 0;

                for (int y = y0; y < y1; y++)
                {
                    int index = (y * sourceWidth + x0) * 4;
                    for (int x = x0; x < x1; x++, index += 4)
                    {
                        r += rgba[index];
                        g += rgba[index + 1];
                        b += rgba[index + 2];
                        count++;
                    }
                }

                int o = (oy * outWidth + ox) * 4;
                output[o] = (byte)(r / count);
                output[o + 1] = (byte)(g / count);
                output[o + 2] = (byte)(b / count);
                output[o + 3] = 255;
            }
        }

        return output;
    }
}
