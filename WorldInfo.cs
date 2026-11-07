using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CuboidFever;

// Where the player was and what they were doing when the world was last saved.
public sealed class PlayerSave
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public float Yaw { get; set; }
    public float Pitch { get; set; }

    public bool Flying { get; set; }
    public int SelectedHotbarSlot { get; set; }

    // False if the numbers cannot be a real position (a hand-edited or damaged file), in which case the world
    // spawns the player the normal way instead. A method rather than a property so it is not written to level.json.
    public bool HasUsablePosition() =>
        float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z) && Y > -1000f && Y < 10000f;
}

// Everything about a world that is not block data: it is level.json in the world's folder.
// Block edits are stored separately, one file per edited column (see docs/SAVE_SYSTEM_ROADMAP.md).
public sealed class WorldInfo
{
    // Bump when level.json changes in a way old builds cannot read, and add a migration for the old number.
    public const int CurrentFormatVersion = 1;
    public const string FileName = "level.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public int FormatVersion { get; set; } = CurrentFormatVersion;

    public string Name { get; set; } = "New World";
    public int Seed { get; set; }

    // The game version that last saved this world, for diagnosing problems with old saves.
    public string GameVersion { get; set; } = GameInfo.Version;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastPlayedUtc { get; set; } = DateTime.UtcNow;

    // Same units as DayNightCycle.TimeOfDay: 0.0 to 1.0, where 0 is sunrise.
    public float TimeOfDay { get; set; } = 0.04f;

    // Null until the world has been saved from inside the game, which means "spawn the normal way".
    public PlayerSave Player { get; set; }

    // Block names in saved-ID order (see BlockPalette). Never reorder or remove entries.
    public List<string> BlockNames { get; set; } = new();

    public static string PathIn(string worldDirectory) => Path.Combine(worldDirectory, FileName);

    // Reads level.json from a world's folder. Throws if it is missing, unreadable, or was written by a newer
    // version of the game than this one (loading it could damage the world).
    public static WorldInfo Load(string worldDirectory)
    {
        string path = PathIn(worldDirectory);

        WorldInfo info;
        try
        {
            info = JsonSerializer.Deserialize<WorldInfo>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{path} is not valid: {e.Message}", e);
        }

        if (info == null)
            throw new InvalidDataException($"{path} is empty.");

        if (info.FormatVersion > CurrentFormatVersion)
            throw new InvalidDataException(
                $"{path} was saved by a newer version of the game (save format {info.FormatVersion}, " +
                $"this build reads up to {CurrentFormatVersion}).");

        info.BlockNames ??= new List<string>();
        return info;
    }

    // Writes level.json into the world's folder (creating the folder if needed) and stamps the save time.
    public void Save(string worldDirectory)
    {
        FormatVersion = CurrentFormatVersion;
        GameVersion = GameInfo.Version;
        LastPlayedUtc = DateTime.UtcNow;

        AtomicFile.WriteAllText(PathIn(worldDirectory), JsonSerializer.Serialize(this, JsonOptions));
    }
}
