using System;

namespace CuboidFever;

/// <summary>
/// Generates small, irregular stone boulders that sit partially embedded
/// in the terrain, with their widest section around ground level.
/// </summary>
public sealed class Boulder : Feature
{
    private const int SpawnOneIn = 300;
    private const int MaxRadius = 2;

    private readonly byte _grassId;
    private readonly byte _dirtId;
    private readonly byte _stoneId;

    public Boulder(BlockRegistry blocks)
    {
        _grassId = blocks.GetId("cuboidfever:grass");
        _dirtId = blocks.GetId("cuboidfever:dirt");
        _stoneId = blocks.GetId("cuboidfever:stone");
    }

    public override string Name => "boulder";
    public override int Salt => 0xB01D3;

    public override void Generate(FeatureContext ctx)
    {
        var rng = ctx.Rng;

        // Leave room around the edges so the boulder remains within
        // the current column.
        for (int x = MaxRadius; x < ctx.Size - MaxRadius; x++)
        for (int z = MaxRadius; z < ctx.Size - MaxRadius; z++)
        {
            if (rng.Next(SpawnOneIn) != 0)
                continue;

            int groundY = ctx.TerrainSurfaceY(
                x, z, _grassId, _dirtId, _stoneId);

            if (groundY < 0 || ctx.GetBlock(x, groundY, z) != _grassId)
                continue;

            // Avoid attempting placement too close to the top of the world.
            if (groundY + 3 >= ctx.Height)
                continue;

            PlaceBoulder(ctx, x, groundY, z, rng);
        }
    }

    private void PlaceBoulder(
        FeatureContext ctx, int x, int groundY, int z, Random rng)
    {
        // Bury the center of the boulder one block into the terrain.
        // The exposed stone begins around the original ground level.
        TryPlaceRock(ctx, x, groundY - 1, z);

        // Widest layer: centered on the terrain surface.
        // Each position follows its own surface height, helping the
        // boulder conform to gentle slopes.
        for (int dx = -MaxRadius; dx <= MaxRadius; dx++)
        for (int dz = -MaxRadius; dz <= MaxRadius; dz++)
        {
            int distance = dx * dx + dz * dz;

            // Trim the footprint into an irregular, roughly rounded shape.
            if (distance > 5)
                continue;

            if (distance >= 5 && rng.Next(3) == 0)
                continue;

            int px = x + dx;
            int pz = z + dz;
            int localGroundY = ctx.TerrainSurfaceY(
                px, pz, _grassId, _dirtId, _stoneId);

            if (localGroundY < 0)
                continue;

            // Replace the surface block with stone, embedding the boulder
            // instead of stacking it entirely above the grass.
            TryPlaceRock(ctx, px, localGroundY, pz);

            // A few extra blocks below the surface give the rock a
            // buried, uneven underside.
            if (distance <= 2 && rng.Next(3) != 0)
                TryPlaceRock(ctx, px, localGroundY - 1, pz);
        }

        // Upper layer: narrower and irregular, leaving the widest part
        // around ground level.
        for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
        {
            if (Math.Abs(dx) == 1 &&
                Math.Abs(dz) == 1 &&
                rng.Next(2) == 0)
                continue;

            TryPlaceRock(ctx, x + dx, groundY + 1, z + dz);
        }

        // Uneven top stones.
        TryPlaceRock(ctx, x, groundY + 2, z);

        if (rng.Next(2) == 0)
        {
            int dx = rng.Next(-1, 2);
            int dz = rng.Next(-1, 2);

            if (dx != 0 || dz != 0)
                TryPlaceRock(ctx, x + dx, groundY + 2, z + dz);
        }
    }

    /// <summary>
    /// Places stone in air or replaces grass, dirt, or stone terrain.
    /// Other block types are left untouched.
    /// </summary>
    private void TryPlaceRock(FeatureContext ctx, int x, int y, int z)
    {
        if (!ctx.InBounds(x, y, z))
            return;

        // Existing terrain can be replaced safely, one block type at a time.
        if (ctx.ReplaceIfBlock(x, y, z, _grassId, _stoneId))
            return;

        if (ctx.ReplaceIfBlock(x, y, z, _dirtId, _stoneId))
            return;

        // Stone already occupies this position, so nothing needs changing.
        if (ctx.GetBlock(x, y, z) == _stoneId)
            return;

        // Above ground, place stone only if the position is empty.
        ctx.PlaceIfAir(x, y, z, _stoneId);
    }
}