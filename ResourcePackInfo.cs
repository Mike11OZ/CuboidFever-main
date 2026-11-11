using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace CuboidFever;

// One resource pack found in the resourcepacks folder: a folder, or a .zip file with the same contents.
//
// A pack holds its textures in the same layout as the game's own assets folder, so a pack can be made by copying
// the game's textures and editing them:
//
//     MyPack/                                         (or MyPack.zip, with assets/ at the top of the zip)
//         assets/cuboidfever/textures/block/stone.png
//         assets/cuboidfever/textures/block/grass_top.png
//
// A pack only needs the textures it changes. Anything it leaves out keeps the game's own texture.
// Textures must be the same size as the game's (16x16 at the moment); others are ignored (TextureAtlas.ApplyPack).
public sealed class ResourcePackInfo
{
    // Where a pack keeps its textures, as a path inside a zip (always forward slashes)
    public const string TextureRoot = "assets/cuboidfever/textures/";
    public const string BlockTextureRoot = TextureRoot + "block/";

    // A texture file bigger than this is ignored. Block textures are tiny, and a pack is a file the player
    // downloaded, so there is no reason to read something huge into memory.
    private const long MaxTextureBytes = 1024 * 1024;

    // The pack's folder or file name. This is what is saved in the options to remember the selection.
    public string Id { get; }

    // What the list shows: the Id without ".zip"
    public string DisplayName { get; }

    // Full path of the pack's folder or zip file
    public string Location { get; }

    public bool IsZip { get; }

    private ResourcePackInfo(string location, bool isZip)
    {
        Location = location;
        IsZip = isZip;
        Id = Path.GetFileName(location);
        DisplayName = isZip ? Path.GetFileNameWithoutExtension(location) : Id;
    }

    // Looks in a folder (normally the resourcepacks folder) for packs, sorted by name. Anything that is not a
    // pack (a stray file, a folder with no textures, a damaged zip) is skipped rather than failing the whole list.
    public static List<ResourcePackInfo> Discover(string root)
    {
        var packs = new List<ResourcePackInfo>();
        if (!Directory.Exists(root)) return packs;

        foreach (string path in Directory.EnumerateFileSystemEntries(root))
        {
            try
            {
                ResourcePackInfo pack =
                    Directory.Exists(path) ? FromFolder(path) :
                    path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? FromZip(path) :
                    null;

                if (pack != null) packs.Add(pack);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                Console.WriteLine($"[Warning] Skipping resource pack '{path}': {e.Message}");
            }
        }

        packs.Sort((a, b) =>
        {
            int byName = string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : string.CompareOrdinal(a.Id, b.Id);
        });

        return packs;
    }

    // A folder is a pack if it has assets/cuboidfever/textures in it. Returns null if not.
    public static ResourcePackInfo FromFolder(string folder) =>
        Directory.Exists(Path.Combine(folder, "assets", "cuboidfever", "textures"))
            ? new ResourcePackInfo(folder, isZip: false)
            : null;

    // A zip is a pack if it has anything under assets/cuboidfever/textures/. Returns null if not.
    // Throws InvalidDataException for a file that is not a readable zip.
    public static ResourcePackInfo FromZip(string file)
    {
        // Opened only for this check and closed again, so a zip is never kept locked while the game runs
        using ZipArchive zip = ZipFile.OpenRead(file);

        foreach (ZipArchiveEntry entry in zip.Entries)
        {
            if (entry.FullName.StartsWith(TextureRoot, StringComparison.OrdinalIgnoreCase))
                return new ResourcePackInfo(file, isZip: true);
        }

        return null;
    }

    // Reads the pack's block textures (the PNG files under assets/cuboidfever/textures/block/) into memory.
    // The key is the texture's name without ".png", with "/" between folders, which is how the texture atlas
    // names them after "cuboidfever:". A file at block/stone.png has the key "stone".
    public Dictionary<string, byte[]> ReadBlockTextures()
    {
        var textures = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        if (IsZip)
        {
            using ZipArchive zip = ZipFile.OpenRead(Location);

            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (entry.Name.Length == 0) continue; // a folder entry
                if (!entry.FullName.StartsWith(BlockTextureRoot, StringComparison.OrdinalIgnoreCase)) continue;
                if (!entry.FullName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                if (entry.Length > MaxTextureBytes)
                {
                    Console.WriteLine($"[Warning] Resource pack '{Id}': '{entry.FullName}' is too large, ignored.");
                    continue;
                }

                using Stream stream = entry.Open();
                using var buffer = new MemoryStream((int)entry.Length);
                stream.CopyTo(buffer);

                textures[TextureKey(entry.FullName.Substring(BlockTextureRoot.Length))] = buffer.ToArray();
            }

            return textures;
        }

        string blockFolder = Path.Combine(Location, "assets", "cuboidfever", "textures", "block");
        if (!Directory.Exists(blockFolder)) return textures;

        foreach (string file in Directory.EnumerateFiles(blockFolder, "*.png", SearchOption.AllDirectories))
        {
            if (new FileInfo(file).Length > MaxTextureBytes)
            {
                Console.WriteLine($"[Warning] Resource pack '{Id}': '{file}' is too large, ignored.");
                continue;
            }

            textures[TextureKey(Path.GetRelativePath(blockFolder, file))] = File.ReadAllBytes(file);
        }

        return textures;
    }

    // "stone.png" -> "stone", "sub\stone.png" -> "sub/stone"
    private static string TextureKey(string relativePath) =>
        Path.ChangeExtension(relativePath, null).Replace('\\', '/');
}
