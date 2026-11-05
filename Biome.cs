using System.Collections.Generic;

namespace CuboidFever;

/// <summary>
/// A biome is just a bundle of per-region settings. For now it only controls
/// the surface blocks; later steps can hang tree/feature choices, height
/// modifiers, grass tint, etc. off the same object.
///
/// Instances are immutable, so they're safe to share between worker threads.
/// </summary>
public sealed class Biome
{
    public string Name { get; }
    public byte TopBlock { get; }
    public byte FillerBlock { get; }

    // Feature name (Feature.Name) -> how common it is here. Missing = 1.
    private readonly IReadOnlyDictionary<string, double> _featureDensity;

    public Biome(string name, byte topBlock, byte fillerBlock,
        IReadOnlyDictionary<string, double> featureDensity = null)
    {
        Name = name;
        TopBlock = topBlock;
        FillerBlock = fillerBlock;
        _featureDensity = featureDensity ?? new Dictionary<string, double>();
    }

    /// <summary>
    /// Multiplier on a feature's spawn chance in this biome: 0 = never,
    /// 1 = default, 2 = twice as common. Features not listed get 1.
    /// </summary>
    public double FeatureDensity(string featureName) =>
        _featureDensity.TryGetValue(featureName, out var d) ? d : 1.0;

    public override string ToString() => Name;
}

/// <summary>
/// The set of biomes the climate map can pick from. Mirrors Beta 1.7's list
/// (minus Hell/Sky/Ice Desert, which the temperature/humidity table never
/// selects). Only Desert differs from the default surface so far.
/// </summary>
public sealed class Biomes
{
    public readonly Biome Rainforest, Swampland, SeasonalForest, Forest, Savanna,
        Shrubland, Taiga, Desert, Plains, Tundra;

    public Biomes(BlockRegistry blocks)
    {
        byte grass = blocks.GetId("cuboidfever:grass");
        byte dirt = blocks.GetId("cuboidfever:dirt");
        byte sand = blocks.GetId("cuboidfever:sand");
        byte mud_dried = blocks.GetId("cuboidfever:mud_dried");
        byte mud = blocks.GetId("cuboidfever:mud");

        // Tree densities: oak, big oak, pine, big pine. These multiply each
        // tree feature's base spawn chance (1 = as before, 0 = none).
        Rainforest = new Biome("Rainforest", grass, dirt, Trees(3.0, 3.0, 0, 0));
        Swampland = new Biome("Swampland", mud, dirt, Trees(1.0, 0.5, 0, 0));
        SeasonalForest = new Biome("Seasonal Forest", grass, dirt, Trees(2.0, 1.5, 0, 0));
        Forest = new Biome("Forest", grass, dirt, Trees(2.5, 2.0, 0, 0));
        Savanna = new Biome("Savanna", grass, dirt, Trees(0.05, 0.1, 0, 0));
        Shrubland = new Biome("Shrubland", grass, dirt, Trees(0.4, 0.2, 0, 0));
        Taiga = new Biome("Taiga", grass, dirt, Trees(0, 0, 2.5, 2.5));
        Desert = new Biome("Desert", sand, sand, Trees(0, 0, 0, 0));
        Plains = new Biome("Plains", grass, dirt, Trees(0.05, 0, 0, 0));
        Tundra = new Biome("Tundra", grass, dirt, Trees(0, 0, 0.2, 0.1));
    }

    private static Dictionary<string, double> Trees(double oak, double bigOak, double pine, double bigPine) => new()
    {
        ["oak_tree"] = oak,
        ["big_oak_tree"] = bigOak,
        ["pine_tree"] = pine,
        ["big_pine_tree"] = bigPine,
    };
}