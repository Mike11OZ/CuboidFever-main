using System;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

public class ThumbnailScalerTests
{
    private static byte[] Solid(int width, int height, byte r, byte g, byte b)
    {
        var data = new byte[width * height * 4];
        for (int i = 0; i < data.Length; i += 4)
        {
            data[i] = r; data[i + 1] = g; data[i + 2] = b; data[i + 3] = 0; // alpha deliberately 0
        }
        return data;
    }

    [Fact]
    public void OutputHasTheRequestedSize()
    {
        var result = ThumbnailScaler.Downscale(Solid(1920, 1080, 1, 2, 3), 1920, 1080, 320, 180);
        Assert.Equal(320 * 180 * 4, result.Length);
    }

    [Fact]
    public void SolidColor_StaysThatColor_AndIsOpaque()
    {
        var result = ThumbnailScaler.Downscale(Solid(64, 36, 10, 200, 30), 64, 36, 16, 9);

        for (int i = 0; i < result.Length; i += 4)
        {
            Assert.Equal<byte>(10, result[i]);
            Assert.Equal<byte>(200, result[i + 1]);
            Assert.Equal<byte>(30, result[i + 2]);
            Assert.Equal<byte>(255, result[i + 3]);
        }
    }

    [Fact]
    public void BlockOfPixels_IsAveraged()
    {
        // 2x2 source, 1x1 output: the average of black, white, black, white
        var source = new byte[]
        {
            0, 0, 0, 255,   255, 255, 255, 255,
            0, 0, 0, 255,   255, 255, 255, 255
        };

        var result = ThumbnailScaler.Downscale(source, 2, 2, 1, 1);

        Assert.Equal<byte>(127, result[0]);
    }

    [Fact]
    public void WideSource_IsCroppedAtTheSidesNotStretched()
    {
        // 8x2 source, 2x2 output: only the middle 2x2 is used. Left and right thirds are red, the middle is blue.
        var source = new byte[8 * 2 * 4];
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                int i = (y * 8 + x) * 4;
                bool middle = x >= 3 && x < 5;
                source[i] = (byte)(middle ? 0 : 255);
                source[i + 2] = (byte)(middle ? 255 : 0);
            }
        }

        var result = ThumbnailScaler.Downscale(source, 8, 2, 2, 2);

        for (int i = 0; i < result.Length; i += 4)
        {
            Assert.Equal<byte>(0, result[i]);
            Assert.Equal<byte>(255, result[i + 2]);
        }
    }

    [Fact]
    public void SmallSource_IsEnlargedWithoutErrors()
    {
        var result = ThumbnailScaler.Downscale(Solid(2, 2, 5, 5, 5), 2, 2, 8, 8);

        Assert.Equal(8 * 8 * 4, result.Length);
        Assert.Equal<byte>(5, result[0]);
    }

    [Fact]
    public void NotEnoughData_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => ThumbnailScaler.Downscale(new byte[10], 4, 4, 2, 2));
    }
}
