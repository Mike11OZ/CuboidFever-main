using System;
using System.IO;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

// Each test gets its own temporary "saves folder" with a world in it.
public sealed class WorldDeletionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cuboidfever-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string MakeWorld(string name)
    {
        string folder = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.Combine(folder, "columns"));
        File.WriteAllText(WorldInfo.PathIn(folder), "{}");
        File.WriteAllBytes(Path.Combine(folder, "columns", "c.0.0.cfc"), new byte[] { 1, 2, 3 });
        return folder;
    }

    [Fact]
    public void World_IsDeletedWithEverythingInIt()
    {
        string world = MakeWorld("My World");

        WorldDeletion.Delete(_root, world);

        Assert.False(Directory.Exists(world));
        Assert.True(Directory.Exists(_root)); // the saves folder itself stays
    }

    [Fact]
    public void OtherWorlds_AreLeftAlone()
    {
        string a = MakeWorld("A");
        string b = MakeWorld("B");

        WorldDeletion.Delete(_root, a);

        Assert.True(File.Exists(WorldInfo.PathIn(b)));
    }

    [Fact]
    public void TrailingSeparatorInThePath_IsFine()
    {
        string world = MakeWorld("W");

        WorldDeletion.Delete(_root, world + Path.DirectorySeparatorChar);

        Assert.False(Directory.Exists(world));
    }

    [Fact]
    public void SavesFolderItself_IsRefused()
    {
        MakeWorld("W");

        Assert.Throws<InvalidOperationException>(() => WorldDeletion.Delete(_root, _root));
        Assert.True(Directory.Exists(_root));
    }

    [Fact]
    public void InnerFolderOfAWorld_IsRefused()
    {
        string world = MakeWorld("W");

        Assert.Throws<InvalidOperationException>(() => WorldDeletion.Delete(_root, Path.Combine(world, "columns")));
        Assert.True(File.Exists(Path.Combine(world, "columns", "c.0.0.cfc")));
    }

    [Fact]
    public void FolderWithoutLevelJson_IsRefused()
    {
        string stranger = Path.Combine(_root, "Not a world");
        Directory.CreateDirectory(stranger);
        File.WriteAllText(Path.Combine(stranger, "notes.txt"), "keep me");

        Assert.Throws<InvalidOperationException>(() => WorldDeletion.Delete(_root, stranger));
        Assert.True(File.Exists(Path.Combine(stranger, "notes.txt")));
    }

    [Fact]
    public void FolderOutsideTheSavesFolder_IsRefused()
    {
        string elsewhere = Path.Combine(Path.GetTempPath(), "cuboidfever-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(elsewhere);
        File.WriteAllText(WorldInfo.PathIn(elsewhere), "{}");

        try
        {
            Assert.Throws<InvalidOperationException>(() => WorldDeletion.Delete(_root, elsewhere));
            Assert.True(Directory.Exists(elsewhere));
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }

    [Fact]
    public void PathWithDotDot_CannotEscapeTheSavesFolder()
    {
        string world = MakeWorld("W");

        // Resolves to the saves folder itself
        Assert.False(WorldDeletion.IsWorldFolder(_root, Path.Combine(world, "..")));
    }

    [Fact]
    public void MissingOrBlankPaths_AreRefused()
    {
        Assert.False(WorldDeletion.IsWorldFolder(_root, Path.Combine(_root, "nope")));
        Assert.False(WorldDeletion.IsWorldFolder(_root, ""));
        Assert.False(WorldDeletion.IsWorldFolder(_root, null));
        Assert.False(WorldDeletion.IsWorldFolder(null, _root));
    }
}
