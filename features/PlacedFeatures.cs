using System;
using System.Collections.Generic;

namespace CuboidFever;

/// <summary>
/// Runs every procedural feature (trees, rocks, fallen logs, grass...) over a
/// freshly generated column, in registration order.
///
/// Pipeline position: terrain shape and surface first (TerrainGenerator), then
/// features, then lighting. Features run in the order listed in the constructor,
/// so later features see what earlier ones placed (e.g. grass tufts can avoid
/// trunks by running after trees).
///
/// Thread safety: columns are generated on worker threads. This class and every
/// registered <see cref="Feature"/> are immutable after construction, and each
/// call builds its own Random and context, so concurrent calls are safe.
/// </summary>
public sealed class PlacedFeatures
{
    private readonly int _seed;
    private readonly byte _airId;
    private readonly ClimateMap _climate;
    private readonly Feature[] _features;

    public PlacedFeatures(int seed, BlockRegistry blocks, ClimateMap climate)
    {
        _seed = seed;
        _climate = climate;
        _airId = blocks.GetId("cuboidfever:air");

        // Registration order is generation order. To add a feature: create its
        // file in Features/, give it a unique Salt, and list it here.
        _features = new Feature[]
        {
            new OakTreeFeature(blocks),
            new BigOakTreeFeature(blocks),
            new PineTreeFeature(blocks),
            new BigPineTreeFeature(blocks),
            new StoneBoulderFeature(blocks),
            new RhyoliteBoulderFeature(blocks),
            new DragonStoneBoulderFeature(blocks),
            new TallGrassFeature(blocks),
            new FlowerFeature(blocks)
        };

        EnsureUniqueSalts(_features);
    }

    public void Generate(int columnX, int columnZ, byte[,,] column)
    {
        // Look up every column's biome once, not once per feature.
        var biomes = new Biome[Chunk.Size, Chunk.Size];
        for (int x = 0; x < Chunk.Size; x++)
        for (int z = 0; z < Chunk.Size; z++)
            biomes[x, z] = _climate.GetBiome(columnX * Chunk.Size + x, columnZ * Chunk.Size + z);

        foreach (var feature in _features)
        {
            var rng = DeterministicRandom.ForFeature(
                _seed,
                feature.Salt,
                columnX,
                columnZ);

            feature.Generate(new FeatureContext(column, columnX, columnZ, _airId, rng, biomes));
        }
    }

    private static void EnsureUniqueSalts(Feature[] features)
    {
        var seen = new Dictionary<int, string>();

        foreach (var f in features)
        {
            if (seen.TryGetValue(f.Salt, out var other))
                throw new InvalidOperationException(
                    $"Features '{other}' and '{f.Name}' share salt 0x{f.Salt:X}; each feature needs a unique salt.");

            seen[f.Salt] = f.Name;
        }
    }
}