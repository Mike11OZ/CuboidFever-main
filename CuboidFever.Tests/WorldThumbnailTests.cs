using System;
using System.IO;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

// The world list picture on disk. Each test gets its own temporary world folder.
public sealed class WorldThumbnailTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "cuboidfever-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void NoThumbnailYet_ReadsAsNull()
    {
        Directory.CreateDirectory(_folder);
        Assert.Null(WorldThumbnail.TryRead(_folder));
    }

    [Fact]
    public void FolderThatDoesNotExist_ReadsAsNull()
    {
        Assert.Null(WorldThumbnail.TryRead(_folder));
    }

    [Fact]
    public void WrittenBytes_ReadBackUnchanged()
    {
        var png = new byte[] { 137, 80, 78, 71, 1, 2, 3 };

        WorldThumbnail.Write(_folder, png);

        Assert.Equal<byte>(png, WorldThumbnail.TryRead(_folder));
    }

    [Fact]
    public void WritingAgain_ReplacesTheOldPicture()
    {
        WorldThumbnail.Write(_folder, new byte[] { 1, 1, 1 });
        WorldThumbnail.Write(_folder, new byte[] { 2, 2 });

        Assert.Equal<byte>(new byte[] { 2, 2 }, WorldThumbnail.TryRead(_folder));
        Assert.False(File.Exists(WorldThumbnail.PathIn(_folder) + ".tmp"));
    }

    [Fact]
    public void EmptyOrMissingBytes_AreIgnored()
    {
        WorldThumbnail.Write(_folder, new byte[] { 9 });

        WorldThumbnail.Write(_folder, Array.Empty<byte>());
        WorldThumbnail.Write(_folder, null);

        Assert.Equal<byte>(new byte[] { 9 }, WorldThumbnail.TryRead(_folder));
    }

    [Fact]
    public void EmptyFileOnDisk_ReadsAsNull()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(WorldThumbnail.PathIn(_folder), Array.Empty<byte>());

        Assert.Null(WorldThumbnail.TryRead(_folder));
    }
}
