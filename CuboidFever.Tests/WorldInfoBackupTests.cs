using System;
using System.IO;
using System.Text;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

// level.json.bak: one damaged or lost level.json must not make a world unopenable.
public sealed class WorldInfoBackupTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "cuboidfever-tests-" + Guid.NewGuid().ToString("N"));

    public WorldInfoBackupTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private void SaveWorld(string name) => new WorldInfo { Name = name, Seed = 7 }.Save(_folder);

    private static void Damage(string path) => File.WriteAllText(path, "this is not json", new UTF8Encoding(false));

    [Fact]
    public void FirstSave_HasNothingToBackUp()
    {
        SaveWorld("One");

        Assert.False(File.Exists(WorldInfo.BackupPathIn(_folder)));
    }

    [Fact]
    public void EachSave_KeepsThePreviousLevelJsonAsTheBackup()
    {
        SaveWorld("One");
        SaveWorld("Two");

        Assert.Equal("Two", WorldInfo.Load(_folder).Name);

        File.Delete(WorldInfo.PathIn(_folder));
        Assert.Equal("One", WorldInfo.Load(_folder).Name); // the backup holds the save before the latest
    }

    [Fact]
    public void DamagedLevelJson_IsReplacedByTheBackup_AndRepaired()
    {
        SaveWorld("One");
        SaveWorld("Two");
        Damage(WorldInfo.PathIn(_folder));

        var loaded = WorldInfo.Load(_folder);

        Assert.Equal("One", loaded.Name);
        Assert.Equal(7, loaded.Seed);
        Assert.Equal("One", WorldInfo.Load(_folder).Name); // level.json is valid again, not just the backup
        Assert.False(File.ReadAllText(WorldInfo.PathIn(_folder)).StartsWith("this"));
    }

    [Fact]
    public void MissingLevelJson_IsRestoredFromTheBackup()
    {
        SaveWorld("One");
        SaveWorld("Two");
        File.Delete(WorldInfo.PathIn(_folder));

        Assert.Equal("One", WorldInfo.Load(_folder).Name);
        Assert.True(File.Exists(WorldInfo.PathIn(_folder)));
    }

    [Fact]
    public void BothFilesDamaged_ThrowsAsBefore()
    {
        SaveWorld("One");
        SaveWorld("Two");
        Damage(WorldInfo.PathIn(_folder));
        Damage(WorldInfo.BackupPathIn(_folder));

        Assert.Throws<InvalidDataException>(() => WorldInfo.Load(_folder));
    }

    [Fact]
    public void NoFiles_StillThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => WorldInfo.Load(_folder));
    }

    [Fact]
    public void SavingOverADamagedFile_DoesNotDestroyAGoodBackup()
    {
        SaveWorld("One");
        SaveWorld("Two");
        Damage(WorldInfo.PathIn(_folder));

        SaveWorld("Three"); // the damaged file must not be copied over the backup of "One"

        File.Delete(WorldInfo.PathIn(_folder));
        Assert.Equal("One", WorldInfo.Load(_folder).Name);
    }

    [Fact]
    public void FileFromANewerGame_IsNotReplacedByAnOlderBackup()
    {
        SaveWorld("One");
        SaveWorld("Two");

        string path = WorldInfo.PathIn(_folder);
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"FormatVersion\": 1", "\"FormatVersion\": 99"), new UTF8Encoding(false));

        var error = Assert.Throws<InvalidDataException>(() => WorldInfo.Load(_folder));
        Assert.Contains("newer", error.Message);
    }

    [Fact]
    public void AFolderWithOnlyABackup_CountsAsAWorld()
    {
        SaveWorld("One");
        SaveWorld("Two");
        File.Delete(WorldInfo.PathIn(_folder));

        Assert.True(WorldInfo.Exists(_folder));
        Assert.False(WorldInfo.Exists(Path.Combine(_folder, "nowhere")));
    }
}
