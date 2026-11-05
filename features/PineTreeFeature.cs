using System;

namespace CuboidFever;

/// <summary>
/// A classic conical pine tree with a straight trunk and several overlapping
/// layers of foliage. Lower layers are wider than upper layers.
/// </summary>
public sealed class PineTreeFeature : Feature
{
    private const int SpawnOneIn = 85;
    private const int MinTrunkHeight = 10;
    private const int TrunkHeightVariance = 4;
    private const int MaxCrownRadius = 3;

    private readonly byte _grassId;
    private readonly byte _logId;
    private readonly byte _leavesId;

    public PineTreeFeature(BlockRegistry blocks)
    {
        _grassId = blocks.GetId("cuboidfever:grass");
        _logId = blocks.GetId("cuboidfever:pine_log");
        _leavesId = blocks.GetId("cuboidfever:pine_needles");
    }

    public override string Name => "pine_tree";
    public override int Salt => 0x51A3E;

    public override void Generate(FeatureContext ctx)
    {
        var rng = ctx.Rng;

        // Leave room for the widest part of the canopy.
        for (int x = MaxCrownRadius; x < ctx.Size - MaxCrownRadius; x++)
        for (int z = MaxCrownRadius; z < ctx.Size - MaxCrownRadius; z++)
        {
            if (!ctx.Roll(x, z, Name, SpawnOneIn))
                continue;

            int groundY = ctx.SurfaceY(x, z);

            if (ctx.GetBlock(x, groundY, z) != _grassId)
                continue;

            int trunkHeight = MinTrunkHeight + rng.Next(TrunkHeightVariance);
            int topY = groundY + trunkHeight;

            if (topY + 2 >= ctx.Height)
                continue;

            PlaceTrunk(ctx, x, groundY, z, trunkHeight);
            PlaceCrown(ctx, x, topY, z);
        }
    }

    private void PlaceTrunk(
        FeatureContext ctx, int x, int groundY, int z, int height)
    {
        for (int i = 1; i <= height; i++)
            ctx.PlaceIfAir(x, groundY + i, z, _logId);
    }

    private void PlaceCrown(FeatureContext ctx, int x, int topY, int z)
    {
        // Seven foliage layers, widening toward the bottom.
        // Every two layers share a radius to create overlapping branch tiers.
        for (int layer = 0; layer <= 6; layer++)
        {
            int y = topY - 5 + layer;
            int radius = MaxCrownRadius - layer / 2;

            for (int dx = -radius; dx <= radius; dx++)
            for (int dz = -radius; dz <= radius; dz++)
            {
                bool corner = Math.Abs(dx) == radius &&
                              Math.Abs(dz) == radius;

                // Trim corners to soften the blocky outline.
                if (corner && (layer % 2 == 0 || radius == 1))
                    continue;

                // Small gaps between some outer leaves make the tiers
                // feel more like branches than a solid pyramid.
                if (radius >= 2 &&
                    Math.Abs(dx) + Math.Abs(dz) == radius + 1 &&
                    ctx.Rng.Next(5) == 0)
                    continue;

                ctx.PlaceIfAir(x + dx, y, z + dz, _leavesId);
            }
        }

        // A small tuft at the very top.
        ctx.PlaceIfAir(x, topY + 1, z, _leavesId);
    }
}