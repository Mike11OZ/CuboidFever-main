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

    public FeatureContext(byte[,,] blocks, int columnX, int columnZ, byte airId, Random rng)
    {
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
}
