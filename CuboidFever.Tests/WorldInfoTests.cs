using System;
using System.Collections.Generic;
using System.IO;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

// level.json: everything about a world except its blocks. Each test gets its own temporary world folder.
public sealed class WorldInfoTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "cuboidfever-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void PlayerState_SurvivesSaveAndLoad()
    {
        var info = new WorldInfo
        {
            Name = "My World",
            Seed = 5,
            Player = new PlayerSave
            {
                X = 12.5f, Y = 70.25f, Z = -300.75f,
                Yaw = 1.25f, Pitch = -0.5f,
                Flying = true,
                SelectedHotbarSlot = 4
            }
        };
        info.Save(_folder);

        var loaded = WorldInfo.Load(_folder);

        Assert.True(loaded.Player != null);
        Assert.Equal(12.5f, loaded.Player.X);
        Assert.Equal(70.25f, loaded.Player.Y);
        Assert.Equal(-300.75f, loaded.Player.Z);
        Assert.Equal(1.25f, loaded.Player.Yaw);
        Assert.Equal(-0.5f, loaded.Player.Pitch);
        Assert.True(loaded.Player.Flying);
        Assert.Equal(4, loaded.Player.SelectedHotbarSlot);
    }

    [Fact]
    public void WorldNeverSavedFromInGame_HasNoPlayer()
    {
        new WorldInfo { Name = "Fresh" }.Save(_folder);

        Assert.Null(WorldInfo.Load(_folder).Player);
    }

    [Fact]
    public void NameSeedTimeAndBlockList_SurviveSaveAndLoad()
    {
        new WorldInfo
        {
            Name = "Everything",
            Seed = 706944,
            TimeOfDay = 0.625f,
            BlockNames = new List<string> { "cuboidfever:air", "cuboidfever:stone" }
        }.Save(_folder);

        var loaded = WorldInfo.Load(_folder);

        Assert.Equal("Everything", loaded.Name);
        Assert.Equal(706944, loaded.Seed);
        Assert.Equal(0.625f, loaded.TimeOfDay);
        Assert.Equal(new[] { "cuboidfever:air", "cuboidfever:stone" }, loaded.BlockNames);
    }

    [Fact]
    public void SavedPosition_IsOnlyUsableWhenItIsARealPosition()
    {
        Assert.True(new PlayerSave { X = 10, Y = 64, Z = -20 }.HasUsablePosition());

        Assert.False(new PlayerSave { X = float.NaN, Y = 64, Z = 0 }.HasUsablePosition());
        Assert.False(new PlayerSave { X = 0, Y = float.PositiveInfinity, Z = 0 }.HasUsablePosition());
        Assert.False(new PlayerSave { X = 0, Y = -5000, Z = 0 }.HasUsablePosition());
        Assert.False(new PlayerSave { X = 0, Y = 50000, Z = 0 }.HasUsablePosition());
    }

    [Fact]
    public void ValidityCheck_IsNotWrittenToTheFile()
    {
        new WorldInfo { Player = new PlayerSave { X = 1, Y = 64, Z = 1 } }.Save(_folder);

        string text = File.ReadAllText(WorldInfo.PathIn(_folder));

        Assert.False(text.Contains("HasUsablePosition"), text);
    }

    [Fact]
    public void WorldFromANewerGame_IsRefused()
    {
        new WorldInfo { Name = "Future" }.Save(_folder);

        string path = WorldInfo.PathIn(_folder);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"FormatVersion\": 1", "\"FormatVersion\": 99"));

        var error = Assert.Throws<InvalidDataException>(() => WorldInfo.Load(_folder));
        Assert.Contains("newer", error.Message);
    }

    [Fact]
    public void DamagedLevelFile_ThrowsInsteadOfReturningNonsense()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(WorldInfo.PathIn(_folder), "{ this is not json");

        Assert.Throws<InvalidDataException>(() => WorldInfo.Load(_folder));
    }
}
