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

    // assets/cuboidfever/models/entity: Blockbench models (.bbmodel) of creatures, and their textures
    public static readonly string EntityModelRoot =
        Path.Combine(AppContext.BaseDirectory, "assets", "cuboidfever", "models", "entity");

    public static readonly string EntityTextureRoot =
        Path.Combine(AppContext.BaseDirectory, "assets", "cuboidfever", "textures", "entity");

    public static readonly string TextureAtlasConfig =Path.Combine(DataRoot, "texture_atlas.json");
    public static readonly string DayNightConfig = Path.Combine(DataRoot, "daynight.json");

    // Where each HUD element sits on the screen (HudLayout)
    public static readonly string HudLayoutConfig = Path.Combine(DataRoot, "hud_layout.json");

    // Everything the player owns lives in the user's app-data folder rather than next to the executable: the
    // build output folder is wiped by a clean or rebuild, and that must never delete saves or resource packs.
    // (Windows: %APPDATA%\CuboidFever, Linux: ~/.config/CuboidFever)
    //
    // SpecialFolderOption.Create matters on Linux and macOS, where GetFolderPath returns an empty string for a
    // folder that does not exist yet (a fresh ~/.config). Without it the path would silently become relative.
    //
    // Declared before the paths below: static fields are initialized in the order they are written.
    private static readonly string AppDataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create),
        "CuboidFever");

    // Saved worlds, one folder per world.
    public static readonly string SavesRoot = Path.Combine(AppDataRoot, "saves");

    // Resource packs: folders or .zip files the player drops in to replace the game's textures (ResourcePackInfo).
    public static readonly string ResourcePackRoot = Path.Combine(AppDataRoot, "resourcepacks");

    // Player options that are saved between runs, such as the selected resource pack (GameOptions).
    public static readonly string OptionsFile = Path.Combine(AppDataRoot, "options.json");
}
