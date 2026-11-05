using System;

namespace CuboidFever;

/// <summary>
/// A tall pine with a long, mostly bare trunk and an irregular crown of
/// foliage concentrated near the top, inspired by mature Southern pines.
/// </summary>
public sealed class BigPineTreeFeature : Feature
{
    private const int SpawnOneIn = 180;
    private const int MinTrunkHeight = 16;
    private const int TrunkHeightVariance = 7;
    private const int CrownHeight = 8;
    private const int MaxCrownRadius = 3;

    private readonly byte _grassId;
    private readonly byte _logId;
    private readonly byte _leavesId;

    public BigPineTreeFeature(BlockRegistry blocks)
    {
        _grassId = blocks.GetId("cuboidfever:grass");
        _logId = blocks.GetId("cuboidfever:pine_log");
        _leavesId = blocks.GetId("cuboidfever:pine_needles");
    }

    public override string Name => "big_pine_tree";
    public override int Salt => 0xB16A3E;

    public override void Generate(FeatureContext ctx)
    {
        var rng = ctx.Rng;

        // The crown can extend three blocks in any horizontal direction.
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
        // The crown begins near the top of the tree. Its lower layers
        // are broad, while its upper layers narrow into a loose tuft.
        int crownBaseY = topY - CrownHeight + 2;

        for (int layer = 0; layer < CrownHeight; layer++)
        {
            int y = crownBaseY + layer;

            // Broad lower crown, narrower upper crown, with irregular tiers.
            int radius;
            if (layer < 2)
                radius = 2;
            else if (layer < 5)
                radius = 3;
            else if (layer < 7)
                radius = 2;
            else
                radius = 1;

            for (int dx = -radius; dx <= radius; dx++)
            for (int dz = -radius; dz <= radius; dz++)
            {
                bool corner = Math.Abs(dx) == radius &&
                              Math.Abs(dz) == radius;

                // Avoid square, solid-looking foliage layers.
                if (corner && ctx.Rng.Next(3) != 0)
                    continue;

                // Thin out the outer edge of the crown.
                bool outerEdge = Math.Abs(dx) == radius ||
                                 Math.Abs(dz) == radius;

                if (outerEdge && ctx.Rng.Next(6) == 0)
                    continue;

                ctx.PlaceIfAir(x + dx, y, z + dz, _leavesId);
            }
        }

        // Add a few short branch stubs beneath the crown. These help
        // break up the outline without filling in the long bare trunk.
        PlaceBranch(ctx, x, topY - 5, z, 1, 0);
        PlaceBranch(ctx, x, topY - 4, z, -1, 0);
        PlaceBranch(ctx, x, topY - 3, z, 0, 1);
        PlaceBranch(ctx, x, topY - 2, z, 0, -1);

        // Finish with a small, irregular top tuft.
        ctx.PlaceIfAir(x, topY + 1, z, _leavesId);
        ctx.PlaceIfAir(x + 1, topY + 1, z, _leavesId);
        ctx.PlaceIfAir(x - 1, topY + 1, z, _leavesId);
        ctx.PlaceIfAir(x, topY + 1, z + 1, _leavesId);
        ctx.PlaceIfAir(x, topY + 1, z - 1, _leavesId);
    }

    private void PlaceBranch(
        FeatureContext ctx, int x, int y, int z, int dx, int dz)
    {
        int branchX = x + dx;
        int branchZ = z + dz;

        // One log block extending from the central trunk.
        ctx.PlaceIfAir(branchX, y, branchZ, _logId);

        // Small leaf cluster around the branch tip.
        ctx.PlaceIfAir(branchX, y + 1, branchZ, _leavesId);
        ctx.PlaceIfAir(branchX + dx, y, branchZ + dz, _leavesId);
        ctx.PlaceIfAir(branchX - dz, y, branchZ + dx, _leavesId);
        ctx.PlaceIfAir(branchX + dz, y, branchZ - dx, _leavesId);
    }
}