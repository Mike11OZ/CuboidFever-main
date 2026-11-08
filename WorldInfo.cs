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

    // The copy of the previous level.json, kept in case the main one is ever lost or damaged
    public const string BackupFileName = FileName + ".bak";

    public static string BackupPathIn(string worldDirectory) => Path.Combine(worldDirectory, BackupFileName);

    // True if the folder has a level.json or at least a backup of one, so it is a world that can be listed
    public static bool Exists(string worldDirectory) =>
        File.Exists(PathIn(worldDirectory)) || File.Exists(BackupPathIn(worldDirectory));

    // Reads one file. Returns the world info, or null with the reason in `error`. `newer` is set when the file is
    // fine but was written by a newer version of the game (see Load: that is never "repaired" from a backup).
    private static WorldInfo Read(string path, out Exception error, out bool newer)
    {
        error = null;
        newer = false;

        WorldInfo info;
        try
        {
            info = JsonSerializer.Deserialize<WorldInfo>(File.ReadAllText(path), JsonOptions);
        }
        catch (JsonException e)
        {
            error = new InvalidDataException($"{path} is not valid: {e.Message}", e);
            return null;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            error = e;
            return null;
        }

        if (info == null)
        {
            error = new InvalidDataException($"{path} is empty.");
            return null;
        }

        if (info.FormatVersion > CurrentFormatVersion)
        {
            newer = true;
            error = new InvalidDataException(
                $"{path} was saved by a newer version of the game (save format {info.FormatVersion}, " +
                $"this build reads up to {CurrentFormatVersion}).");
            return null;
        }

        info.BlockNames ??= new List<string>();
        return info;
    }

    // Reads level.json from a world's folder. Throws if it is missing, unreadable, or was written by a newer
    // version of the game than this one (loading it could damage the world).
    //
    // If level.json is missing or damaged but level.json.bak is fine, the backup is used instead (and put back as
    // level.json), so one bad file does not make a world unopenable. A file from a newer game is not treated as
    // damaged: falling back to an older backup would quietly drop that game's changes.
    public static WorldInfo Load(string worldDirectory)
    {
        string path = PathIn(worldDirectory);

        WorldInfo info = Read(path, out Exception error, out bool newer);
        if (info != null) return info;
        if (newer) throw error;

        string backupPath = BackupPathIn(worldDirectory);
        if (File.Exists(backupPath))
        {
            WorldInfo backup = Read(backupPath, out _, out _);
            if (backup != null)
            {
                Console.Error.WriteLine($"[Save] {path} could not be read ({error.Message}); using the backup instead.");

                try
                {
                    AtomicFile.WriteAllText(path, JsonSerializer.Serialize(backup, JsonOptions));
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Console.Error.WriteLine($"[Save] Could not restore {path} from the backup: {e.Message}");
                }

                return backup;
            }
        }

        throw error;
    }

    // Writes level.json into the world's folder (creating the folder if needed) and stamps the save time.
    // The level.json being replaced is first kept as level.json.bak, but only if it is itself readable, so a
    // damaged file can never overwrite a good backup.
    public void Save(string worldDirectory)
    {
        FormatVersion = CurrentFormatVersion;
        GameVersion = GameInfo.Version;
        LastPlayedUtc = DateTime.UtcNow;

        string path = PathIn(worldDirectory);

        if (File.Exists(path) && Read(path, out _, out _) != null)
        {
            try
            {
                AtomicFile.WriteAllBytes(BackupPathIn(worldDirectory), File.ReadAllBytes(path));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // The backup is a bonus: failing to make one must not stop the real save
                Console.Error.WriteLine($"[Save] Could not update the level.json backup: {e.Message}");
            }
        }

        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}
