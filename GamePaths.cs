using System;
using System.IO;

namespace CuboidFever;

// Where the game's data and asset files live, relative to the executable.
public static class GamePaths
{
    // data/cuboidfever: block definitions, texture atlas layout, day/night settings
    public static readonly string DataRoot =
        Path.Combine(AppContext.BaseDirectory, "data", "cuboidfever");

    // assets/cuboidfever/textures/block: the individual block texture images
    public static readonly string BlockTextureRoot =
        Path.Combine(AppContext.BaseDirectory, "assets", "cuboidfever", "textures", "block");

    public static readonly string TextureAtlasConfig = Path.Combine(DataRoot, "texture_atlas.json");
    public static readonly string DayNightConfig = Path.Combine(DataRoot, "daynight.json");

    // Saved worlds, one folder per world. Kept in the user's app-data folder rather than next to the
    // executable: the build output folder is wiped by a clean or rebuild, and that must never delete saves.
    // (Windows: %APPDATA%\CuboidFever\saves, Linux: ~/.config/CuboidFever/saves)
    public static readonly string SavesRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CuboidFever", "saves");
}
