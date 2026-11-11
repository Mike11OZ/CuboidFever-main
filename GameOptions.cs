using System;
using System.IO;
using System.Text.Json;

namespace CuboidFever;

// Player choices that are remembered between runs, saved as options.json in the app-data folder.
// Only the selected resource pack for now; other settings can be added here as they need saving.
public sealed class GameOptions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    // The Id of the selected resource pack (ResourcePackInfo.Id). Null means the game's own textures.
    public string ResourcePack { get; set; }

    // Reads the options file. A missing or damaged file gives the defaults: losing a setting is better than
    // the game refusing to start.
    public static GameOptions Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<GameOptions>(File.ReadAllText(path), JsonOptions) ?? new GameOptions();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            Console.WriteLine($"[Warning] Could not read options '{path}', using defaults: {e.Message}");
        }

        return new GameOptions();
    }

    public void Save(string path) =>
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
}
