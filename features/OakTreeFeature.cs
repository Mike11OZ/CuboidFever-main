using System;

namespace CuboidFever;

/// <summary>
/// Small oak tree: a straight 4-6 block trunk with a four-layer blob canopy
/// (two radius-1 layers on top, two radius-2 layers below, corners randomly
/// trimmed). Same silhouette as vanilla's StraightTrunkPlacer + BlobFoliagePlacer.
/// </summary>
public sealed class OakTreeFeature : Feature
{
    // ---- Tunables ----
    private const int SpawnOneIn = 64;        // 1-in-N chance per eligible grass cell
    private const int MinTrunkHeight = 5;
    private const int TrunkHeightVariance = 3; // trunk = Min + [0, Variance)
    private const int LowerCrownRadius = 3;
    private const int UpperCrownRadius = 2;

    private readonly byte _grassId;
    private readonly byte _logId;
    private readonly byte _leavesId;

    public OakTreeFeature(BlockRegistry blocks)
    {
        _grassId = blocks.GetId("cuboidfever:grass");
        _logId = blocks.GetId("cuboidfever:log");
        _leavesId = blocks.GetId("cuboidfever:leaves");
    }

    public override string Name => "oak_tree";

    // Kept at the value the old inline tree code used, so existing seeds
    // produce exactly the same forests after the refactor.
    public override int Salt => 0x5EED;

    public override void Generate(FeatureContext ctx)
    {
        var rng = ctx.Rng;

        // Keep a margin so the widest canopy layer never leaves this column.
        int min = LowerCrownRadius;
        int max = ctx.Size - LowerCrownRadius;

        for (int x = min; x < max; x++)
        for (int z = min; z < max; z++)
        {
            if (!ctx.Roll(x, z, Name, SpawnOneIn))
                continue;

            int y = ctx.SurfaceY(x, z);

            // Water and sand fail this check, preventing seabed trees.
            if (ctx.GetBlock(x, y, z) != _grassId)
                continue;

            int height = MinTrunkHeight + rng.Next(TrunkHeightVariance);

            if (y + height + 2 >= ctx.Height)
                continue;

            PlaceTrunk(ctx, x, y, z, height);
            PlaceCrown(ctx, x, y + height, z);
        }
    }

    private void PlaceTrunk(FeatureContext ctx, int x, int groundY, int z, int height)
    {
        for (int i = 1; i <= height; i++)
            ctx.PlaceIfAir(x, groundY + i, z, _logId);
    }

    // topY is the Y of the highest log.
    private void PlaceCrown(FeatureContext ctx, int x, int topY, int z)
    {
        var rng = ctx.Rng;

        for (int ly = topY - 2; ly <= topY + 1; ly++)
        {
            int r = ly >= topY ? UpperCrownRadius : LowerCrownRadius;

            for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
            {
                bool corner = Math.Abs(dx) == r && Math.Abs(dz) == r;

                // Top layers always lose their corners; lower layers lose
                // them half the time. Short-circuit order matters: it keeps
                // the RNG call sequence identical to the original code.
                if (corner && (ly >= topY || rng.Next(2) == 0))
                    continue;

                ctx.PlaceIfAir(x + dx, ly, z + dz, _leavesId);
            }
        }
    }
}
