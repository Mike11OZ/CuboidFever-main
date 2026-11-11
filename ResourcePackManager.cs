using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// Knows which resource packs are in the resourcepacks folder, which one is selected, and applies the selection
// to the texture atlas. The selection is remembered in options.json.
//
// Selecting a pack takes effect immediately, even in a running world (see TextureAtlas.ApplyPack).
public sealed class ResourcePackManager
{
    private readonly GraphicsDevice _device;
    private readonly string _optionsPath;
    private readonly GameOptions _options;
    private List<ResourcePackInfo> _packs = new();

    // The folder players drop packs into
    public string FolderPath { get; }

    // The packs found by the last Refresh, sorted by name
    public IReadOnlyList<ResourcePackInfo> Packs => _packs;

    // The Id of the pack in use, or null for the game's own textures
    public string ActiveId { get; private set; }

    public ResourcePackManager(GraphicsDevice device, string folderPath, string optionsPath)
    {
        _device = device;
        FolderPath = folderPath;
        _optionsPath = optionsPath;
        _options = GameOptions.Load(optionsPath);

        // Made up front so a player can find the folder and drop packs into it before ever opening the menu
        try
        {
            Directory.CreateDirectory(folderPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"[Warning] Could not create the resource pack folder '{folderPath}': {e.Message}");
        }
    }

    public bool IsActive(ResourcePackInfo pack) => pack.Id == ActiveId;

    // Looks at the folder again, so a pack dropped in while the game is running shows up without a restart.
    public void Refresh()
    {
        _packs = ResourcePackInfo.Discover(FolderPath);

        // The pack in use was deleted while the game ran: go back to the game's own textures
        if (ActiveId != null && !_packs.Exists(p => p.Id == ActiveId))
            Select(null);
    }

    // Applies the pack chosen last time, once at startup after the atlas exists. If that pack is gone the game's
    // own textures are used, and the choice stays saved in case the pack comes back.
    public void ApplySaved()
    {
        Refresh();

        string savedId = _options.ResourcePack;
        if (string.IsNullOrEmpty(savedId)) return;

        ResourcePackInfo saved = _packs.Find(p => p.Id == savedId);
        if (saved == null)
        {
            Console.WriteLine($"[Warning] Resource pack '{savedId}' was not found, using the default textures.");
            return;
        }

        Activate(saved);
    }

    // Uses this pack from now on and remembers it for next time. Null means the game's own textures.
    public void Select(ResourcePackInfo pack)
    {
        Activate(pack);

        _options.ResourcePack = ActiveId;
        try
        {
            _options.Save(_optionsPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"[Warning] Could not save the resource pack choice: {e.Message}");
        }
    }

    // Draws the pack's textures into the atlas. A pack that cannot be read leaves the game's own textures in use.
    private void Activate(ResourcePackInfo pack)
    {
        TextureAtlas atlas = TextureAtlas.Instance
            ?? throw new InvalidOperationException("Initialize TextureAtlas before applying a resource pack.");

        Dictionary<string, byte[]> textures = null;

        if (pack != null)
        {
            try
            {
                textures = pack.ReadBlockTextures();
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Console.WriteLine($"[Warning] Could not read resource pack '{pack.Id}', using the default textures: {e.Message}");
                pack = null;
            }
        }

        int replaced = atlas.ApplyPack(_device, textures);
        ActiveId = pack?.Id;

        Console.WriteLine(pack == null
            ? "Resource pack: default textures."
            : $"Resource pack '{pack.Id}': replaced {replaced} of {textures.Count} texture(s).");
    }

    // Opens the resourcepacks folder in the system's file browser, for the "Open Pack Folder" button.
    public void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(FolderPath);

            ProcessStartInfo start;
            if (OperatingSystem.IsWindows())
            {
                start = new ProcessStartInfo(FolderPath) { UseShellExecute = true };
            }
            else
            {
                start = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open");
                start.ArgumentList.Add(FolderPath);
            }

            Process.Start(start)?.Dispose();
        }
        catch (Exception e)
        {
            // Whatever goes wrong here (no file browser installed, for one), the game carries on
            Console.WriteLine($"[Warning] Could not open '{FolderPath}': {e.Message}");
        }
    }
}
