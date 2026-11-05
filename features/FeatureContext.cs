using System;

namespace CuboidFever;

/// <summary>
/// Everything a <see cref="Feature"/> needs to decorate one column: the block
/// array, the column's position, a deterministic RNG, and a few safe helpers.
/// Created per feature per column by <see cref="PlacedFeatures"/>.
/// </summary>
public sealed class FeatureContext
{
    private readonly byte[,,] _blocks; // [x, y, z]
    private readonly byte _airId;
    private readonly Biome[,] _biomes; // [x, z], one per block column

    public FeatureContext(byte[,,] blocks, int columnX, int columnZ, byte airId, Random rng, Biome[,] biomes)
    {
        _biomes = biomes;
        _blocks = blocks;
        ColumnX = columnX;
        ColumnZ = columnZ;
        _airId = airId;
        Rng = rng;
    }

    /// <summary>Column coordinates (world X / 16, world Z / 16).</summary>
    public int ColumnX { get; }
    public int ColumnZ { get; }

    /// <summary>Deterministic per (world seed, feature, column).</summary>
    public Random Rng { get; }

    /// <summary>Biome at a local (x, z) in this column.</summary>
    public Biome BiomeAt(int x, int z) => _biomes[x, z];

    /// <summary>
    /// The standard "should I spawn here?" roll: a 1-in-<paramref name="baseOneIn"/>
    /// chance, scaled by how common this feature is in the local biome
    /// (see <see cref="Biome.FeatureDensity"/>). Density 0 never spawns, 1 is
    /// the unmodified chance, 2 is twice as likely. Draws the same single
    /// random number as the old inline roll when density is 1, so unaffected
    /// areas keep their exact layout.
    /// </summary>
    public bool Roll(int x, int z, string featureName, int baseOneIn)
    {
        double density = _biomes[x, z].FeatureDensity(featureName);

        if (density <= 0)
            return false;

        int oneIn = density == 1.0
            ? baseOneIn
            : Math.Max(1, (int)Math.Round(baseOneIn / density));

        return Rng.Next(oneIn) == 0;
    }

    public int Size => Chunk.Size;
    public int Height => World.Height;

    public int WorldX(int localX) => ColumnX * Chunk.Size + localX;
    public int WorldZ(int localZ) => ColumnZ * Chunk.Size + localZ;

    public bool InBounds(int x, int y, int z) =>
        (uint)x < (uint)Chunk.Size &&
        (uint)z < (uint)Chunk.Size &&
        (uint)y < (uint)World.Height;

    public byte GetBlock(int x, int y, int z) => _blocks[x, y, z];

    /// <summary>
    /// Y of the highest non-air block at (x, z). Water counts as non-air, so
    /// callers that want dry land should check the returned block's type.
    /// Returns 0 if the whole column is air.
    /// </summary>
    public int SurfaceY(int x, int z)
    {
        int y = World.Height - 1;

        while (y > 0 && _blocks[x, y, z] == _airId)
            y--;

        return y;
    }
    /// <summary>
    /// Finds the highest terrain block at (x, z), ignoring leaves, logs,
    /// water, and other decorations. Returns -1 if no terrain is found.
    /// </summary>
    public int TerrainSurfaceY(
        int x,
        int z,
        byte grassId,
        byte dirtId,
        byte stoneId)
    {
        if ((uint)x >= (uint)Chunk.Size ||
            (uint)z >= (uint)Chunk.Size)
        {
            return -1;
        }

        for (int y = World.Height - 1; y >= 0; y--)
        {
            byte id = _blocks[x, y, z];

            if (id == grassId || id == dirtId || id == stoneId)
                return y;
        }

        return -1;
    }

    /// <summary>
    /// Places <paramref name="id"/> only if the target is inside the column and
    /// currently air. Returns whether a block was placed.
    /// </summary>
    public bool PlaceIfAir(int x, int y, int z, byte id)
    {
        if (!InBounds(x, y, z) || _blocks[x, y, z] != _airId)
            return false;

        _blocks[x, y, z] = id;
        return true;
    }

    /// <summary>
    /// Replaces a block only if the target is inside the column and currently
    /// contains the expected block. Returns whether the replacement succeeded.
    /// </summary>
    public bool ReplaceIfBlock(
        int x, int y, int z, byte expectedId, byte replacementId)
    {
        if (!InBounds(x, y, z) || _blocks[x, y, z] != expectedId)
            return false;

        _blocks[x, y, z] = replacementId;
        return true;
    }
}