using System;
using System.Collections.Generic;
using System.IO;

namespace CuboidFever;

// Translates between a world's saved block IDs and this build's runtime block IDs.
//
// Runtime IDs are handed out alphabetically by BlockRegistry, so adding or removing any block file shifts
// the IDs of every block after it. A save that stored runtime IDs would silently turn into the wrong blocks.
// Instead each world keeps its own list of block names, and a block's position in that list is its saved ID.
//
//   - A world's palette only ever grows: blocks the registry has that the palette lacks are appended at the end.
//   - Entries are never removed or reordered, so a saved ID means the same block for the life of the world.
//   - A block the palette lists but the registry no longer has keeps its slot and loads as air.
//   - Saved ID 0 is always cuboidfever:air, same as the runtime ID.
public sealed class BlockPalette
{
    private const string AirName = "cuboidfever:air";

    private readonly List<string> _names;
    private readonly byte[] _toSaved = new byte[256];    // runtime ID -> saved ID
    private readonly byte[] _toRuntime = new byte[256];  // saved ID -> runtime ID

    private BlockPalette(List<string> names, List<string> missing)
    {
        _names = names;
        MissingBlocks = missing;
    }

    // Block names in saved-ID order. This is what gets written to level.json.
    public IReadOnlyList<string> Names => _names;

    // Blocks the saved palette lists that this build does not define. They load as air.
    public IReadOnlyList<string> MissingBlocks { get; }

    // True when the palette gained entries while loading (this build has blocks the world had not seen yet),
    // so level.json should be rewritten with the longer list.
    public bool NeedsSave { get; private set; }

    public byte ToSaved(byte runtimeId) => _toSaved[runtimeId];
    public byte ToRuntime(byte savedId) => _toRuntime[savedId];

    // A palette for a brand new world: every block in the registry, in runtime ID order.
    public static BlockPalette ForNewWorld(BlockRegistry registry) =>
        Build(registry, Array.Empty<string>());

    // A palette for an existing world, from the names stored in its level.json.
    public static BlockPalette FromSaved(BlockRegistry registry, IReadOnlyList<string> savedNames) =>
        Build(registry, savedNames);

    private static BlockPalette Build(BlockRegistry registry, IReadOnlyList<string> savedNames)
    {
        var names = new List<string>(savedNames);
        var missing = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (names.Count > 0 && !names[0].Equals(AirName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Saved block palette must start with {AirName}.");

        var palette = new BlockPalette(names, missing);

        for (int saved = 0; saved < names.Count; saved++)
        {
            if (!seen.Add(names[saved]))
                throw new InvalidDataException($"Saved block palette lists '{names[saved]}' twice.");

            if (registry.TryGetId(names[saved], out byte runtime))
            {
                palette._toRuntime[saved] = runtime;
                palette._toSaved[runtime] = (byte)saved;
            }
            else
            {
                palette._toRuntime[saved] = Block.Air;
                missing.Add(names[saved]);
            }
        }

        // Blocks the world has not seen yet go on the end, in runtime ID order.
        int originalCount = names.Count;
        for (int runtime = 0; runtime < registry.Count; runtime++)
        {
            string name = registry.GetName((byte)runtime);
            if (seen.Contains(name)) continue;

            if (names.Count >= 256)
                throw new InvalidDataException(
                    "Block palette is full: 256 blocks is the most a world can hold (saved block IDs are one byte).");

            seen.Add(name);
            names.Add(name);
            palette._toRuntime[names.Count - 1] = (byte)runtime;
            palette._toSaved[runtime] = (byte)(names.Count - 1);
        }

        palette.NeedsSave = names.Count != originalCount;
        return palette;
    }
}
