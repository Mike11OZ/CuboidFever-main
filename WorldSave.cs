using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace CuboidFever;

// One saved world on disk, and the only thing the rest of the game talks to about saving.
//
//   <SavesRoot>/<folder>/level.json       name, seed, time of day, block palette (see WorldInfo)
//   <SavesRoot>/<folder>/columns/*.cfc    one file per column the player has changed (see FileColumnStore)
//   <SavesRoot>/<folder>/corrupt/*.cfc    damaged column files that were set aside instead of being deleted
//
// World uses just TryLoadColumn and SaveColumn, so when the game is split into a client and a server, this
// object moves to the server side and nothing else about saving has to change.
public sealed class WorldSave : IDisposable
{
    public string FolderPath { get; }
    public WorldInfo Info { get; }
    public BlockPalette Palette { get; }

    private readonly FileColumnStore _store;
    private readonly ColumnWriteQueue _writer;
    private readonly WorldLock _lock; // held for as long as the world is open, so no other copy of the game can open it

    private WorldSave(string folderPath, WorldInfo info, BlockPalette palette, WorldLock worldLock)
    {
        FolderPath = folderPath;
        Info = info;
        Palette = palette;
        _lock = worldLock;

        _store = new FileColumnStore(folderPath);
        _writer = new ColumnWriteQueue(_store);
    }

    // =====================  CREATING, OPENING, LISTING  =====================

    // Makes the folder and level.json for a brand new world.
    public static WorldSave CreateNew(string name, int seed)
    {
        string root = GamePaths.SavesRoot;
        Directory.CreateDirectory(root);

        string folder = WorldFolderNames.Make(name, n => Directory.Exists(Path.Combine(root, n)));
        string folderPath = Path.Combine(root, folder);
        Directory.CreateDirectory(folderPath);

        var worldLock = WorldLock.Acquire(folderPath);

        try
        {
            var palette = BlockPalette.ForNewWorld(BlockRegistry.Instance);
            var info = new WorldInfo
            {
                Name = name,
                Seed = seed,
                BlockNames = new List<string>(palette.Names)
            };
            info.Save(folderPath);

            return new WorldSave(folderPath, info, palette, worldLock);
        }
        catch
        {
            worldLock.Dispose(); // do not keep a world locked that failed to open
            throw;
        }
    }

    // Opens an existing world. Throws if level.json is missing, damaged, or from a newer version of the game, or
    // WorldInUseException if another copy of the game has the world open.
    public static WorldSave Open(string folderPath)
    {
        // Locked first, so a world that is open elsewhere is never read or rewritten from here
        var worldLock = WorldLock.Acquire(folderPath);

        try
        {
            var info = WorldInfo.Load(folderPath);
            var palette = BlockPalette.FromSaved(BlockRegistry.Instance, info.BlockNames);

            if (palette.MissingBlocks.Count > 0)
            {
                Console.Error.WriteLine(
                    $"[Save] '{info.Name}' uses blocks this version of the game does not have; they will load as air: " +
                    string.Join(", ", palette.MissingBlocks));
            }

            // This version has blocks the world had not seen yet: remember them so their saved IDs never change
            if (palette.NeedsSave)
            {
                info.BlockNames = new List<string>(palette.Names);
                info.Save(folderPath);
            }

            return new WorldSave(folderPath, info, palette, worldLock);
        }
        catch
        {
            worldLock.Dispose(); // do not keep a world locked that failed to open
            throw;
        }
    }

    // Every readable world in the saves folder, most recently played first. Folders without a readable
    // level.json are skipped.
    public static List<(string FolderPath, WorldInfo Info)> ListWorlds()
    {
        var worlds = new List<(string FolderPath, WorldInfo Info)>();

        string root = GamePaths.SavesRoot;
        if (!Directory.Exists(root)) return worlds;

        foreach (string folder in Directory.EnumerateDirectories(root))
        {
            if (!WorldInfo.Exists(folder)) continue;

            try
            {
                worlds.Add((folder, WorldInfo.Load(folder)));
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"[Save] Skipping '{folder}': {e.Message}");
            }
        }

        worlds.Sort((a, b) => b.Info.LastPlayedUtc.CompareTo(a.Info.LastPlayedUtc));
        return worlds;
    }

    // Permanently deletes a world from the saves folder (see WorldDeletion for what is refused). The world must
    // not be open: this is for the world list, which only exists while no world is running.
    public static void DeleteWorld(string folderPath) => WorldDeletion.Delete(GamePaths.SavesRoot, folderPath);

    // "World 1", "World 2", ...: the first one no existing world is using.
    public static string NextDefaultName()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var world in ListWorlds())
            used.Add(world.Info.Name);

        for (int n = 1; ; n++)
        {
            string name = $"World {n}";
            if (!used.Contains(name)) return name;
        }
    }

    // =====================  COLUMNS  =====================

    // The saved version of a column, ready to use except for its light (the caller computes that), or null if
    // the player never changed this column and it should be generated from the seed. Runs on world worker threads.
    //
    // A damaged column file is set aside in the world's "corrupt" folder and reported as not saved, so the world
    // regenerates it instead of failing to load, and the damaged data is not destroyed.
    public Column TryLoadColumn(int cx, int cz)
    {
        byte[] bytes = _writer.ReadThrough(cx, cz);
        if (bytes == null) return null;

        try
        {
            return ColumnSerializer.Read(bytes, Palette.ToRuntimeTable, cx, cz);
        }
        catch (InvalidDataException e)
        {
            SetAsideDamagedColumn(cx, cz, bytes, e);
            return null;
        }
    }

    // Serializes the column now (call from the main thread, where its blocks cannot change mid-write) and
    // queues the bytes for the background writer.
    public void SaveColumn(Column column)
    {
        byte[] bytes = ColumnSerializer.Write(column, Palette.ToSavedTable);
        _writer.Queue(column.CX, column.CZ, bytes);
    }

    private void SetAsideDamagedColumn(int cx, int cz, byte[] bytes, Exception reason)
    {
        Console.Error.WriteLine($"[Save] Column ({cx}, {cz}) is damaged and will be regenerated: {reason.Message}");

        try
        {
            string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            string copy = Path.Combine(FolderPath, "corrupt", FormattableString.Invariant($"c.{cx}.{cz}.{stamp}.cfc"));

            AtomicFile.WriteAllBytes(copy, bytes);
            _store.Delete(cx, cz);

            Console.Error.WriteLine($"[Save] A copy of the damaged file was kept: {copy}");
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"[Save] Could not set the damaged column aside: {e.Message}");
        }
    }

    // =====================  LEVEL, FLUSH, CLOSE  =====================

    // Rewrites level.json from Info (and stamps the time). Small, so it is written right away.
    public void SaveLevel() => Info.Save(FolderPath);

    // Replaces the world's list picture (PNG bytes). Small and written right away; a failure only costs the
    // picture, so it is logged and never thrown at the caller.
    public void SaveThumbnail(byte[] png)
    {
        try
        {
            WorldThumbnail.Write(FolderPath, png);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"[Save] Could not write the thumbnail: {e.Message}");
        }
    }

    // Waits until every queued column is on disk. False means at least one could not be written (see the log).
    public bool Flush() => _writer.Flush();

    // Writes everything still queued, stops the background writer, and lets other copies of the game open the world.
    public void Dispose()
    {
        _writer.Dispose();
        _lock.Dispose();
    }
}
