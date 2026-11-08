using System;
using System.IO;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

public sealed class WorldLockTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "cuboidfever-tests-" + Guid.NewGuid().ToString("N"));

    public WorldLockTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void SecondLock_OnTheSameWorld_IsRefused()
    {
        using var first = WorldLock.Acquire(_folder);

        Assert.Throws<WorldInUseException>(() => WorldLock.Acquire(_folder));
    }

    [Fact]
    public void AfterTheLockIsReleased_TheWorldCanBeLockedAgain()
    {
        var first = WorldLock.Acquire(_folder);
        first.Dispose();

        using var second = WorldLock.Acquire(_folder);
    }

    [Fact]
    public void DifferentWorlds_DoNotBlockEachOther()
    {
        string other = Path.Combine(_folder, "other");
        Directory.CreateDirectory(other);

        using var a = WorldLock.Acquire(_folder);
        using var b = WorldLock.Acquire(other);
    }

    [Fact]
    public void ReleasingTwice_IsHarmless()
    {
        var held = WorldLock.Acquire(_folder);

        held.Dispose();
        held.Dispose();
    }

    [Fact]
    public void TheLockFileStaysInTheFolder_SoItCanBeReused()
    {
        WorldLock.Acquire(_folder).Dispose();

        Assert.True(File.Exists(WorldLock.PathIn(_folder)));
    }
}
