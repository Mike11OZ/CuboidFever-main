using System;

namespace CuboidFever;

/// <summary>
/// Large oak tree with a thick trunk, spreading branches, and a broad,
/// irregular canopy made from several overlapping leaf clusters.
///
/// The overall shape is inspired by Minecraft's large oak trees.
/// All blocks are placed through FeatureContext, using the same
/// placement conventions as OakTreeFeature.
/// </summary>
public sealed class BigOakTreeFeature : Feature
{
    // ---- Tunables ----

    // Large oaks should be rarer than ordinary oak trees.
    private const int SpawnOneIn = 128;

    private const int MinTrunkHeight = 10;
    private const int TrunkHeightVariance = 2;

    private const int TrunkWidth = 1;

    // Maximum radius of a foliage cluster.
    private const int MaxLeafRadius = 3;

    // Branches extend outward from the upper trunk.
    private const int MinBranchLength = 3;
    private const int BranchLengthVariance = 2;

    private readonly byte _grassId;
    private readonly byte _logId;
    private readonly byte _leavesId;

    public BigOakTreeFeature(BlockRegistry blocks)
    {
        _grassId = blocks.GetId("cuboidfever:grass");
        _logId = blocks.GetId("cuboidfever:log");
        _leavesId = blocks.GetId("cuboidfever:leaves");
    }

    public override string Name => "big_oak_tree";

    // Keep this feature's random stream separate from the small oak feature.
    public override int Salt => 0xB10A0;

    public override void Generate(FeatureContext ctx)
    {
        var rng = ctx.Rng;

        // Keep the complete tree inside the feature's horizontal area.
        // The trunk, branches, and outer foliage can extend up to about
        // five blocks from the tree's origin.
        const int margin = 5;

        int min = margin;
        int max = ctx.Size - margin;

        for (int x = min; x < max; x++)
        for (int z = min; z < max; z++)
        {
            if (!ctx.Roll(x, z, Name, SpawnOneIn))
                continue;

            int groundY = ctx.SurfaceY(x, z);

            // Only grow on grass.
            if (ctx.GetBlock(x, groundY, z) != _grassId)
                continue;

            int trunkHeight =
                MinTrunkHeight + rng.Next(TrunkHeightVariance);

            int topY = groundY + trunkHeight;

            // Leave room for the upper branches and foliage.
            if (topY + 5 >= ctx.Height)
                continue;

            // Avoid starting a large tree in an obstructed space.
            if (!CanPlaceTrunk(ctx, x, groundY, z, trunkHeight))
                continue;

            PlaceTrunk(ctx, x, groundY, z, trunkHeight);

            PlaceBranches(ctx, x, groundY, z, trunkHeight);

            PlaceCanopy(ctx, x, topY, z);
        }
    }

    // -----------------------------------------------------------------
    // Trunk
    // -----------------------------------------------------------------

    private bool CanPlaceTrunk(
        FeatureContext ctx,
        int x,
        int groundY,
        int z,
        int height)
    {
        // Check the entire 2x2 trunk footprint before placing anything.
        for (int dy = 1; dy <= height; dy++)
        for (int dx = 0; dx < TrunkWidth; dx++)
        for (int dz = 0; dz < TrunkWidth; dz++)
        {
            if (ctx.GetBlock(x + dx, groundY + dy, z + dz) != 0)
                return false;
        }

        return true;
    }

    private void PlaceTrunk(
        FeatureContext ctx,
        int x,
        int groundY,
        int z,
        int height)
    {
        for (int dy = 1; dy <= height; dy++)
        for (int dx = 0; dx < TrunkWidth; dx++)
        for (int dz = 0; dz < TrunkWidth; dz++)
        {
            ctx.PlaceIfAir(
                x + dx,
                groundY + dy,
                z + dz,
                _logId);
        }
    }

    // -----------------------------------------------------------------
    // Branches
    // -----------------------------------------------------------------

    private void PlaceBranches(
        FeatureContext ctx,
        int x,
        int groundY,
        int z,
        int trunkHeight)
    {
        var rng = ctx.Rng;

        // Randomize the directions so every tree has a slightly
        // different branch arrangement.
        int rotation = rng.Next(4);

        // Four principal branches, approximately evenly distributed
        // around the trunk.
        for (int i = 0; i < 4; i++)
        {
            int direction = (i + rotation) % 4;

            int dx = 0;
            int dz = 0;

            switch (direction)
            {
                case 0:
                    dx = 1;
                    break;

                case 1:
                    dz = 1;
                    break;

                case 2:
                    dx = -1;
                    break;

                case 3:
                    dz = -1;
                    break;
            }

            // Branches begin at different heights on the trunk.
            int startHeight =
                trunkHeight / 2 + rng.Next(Math.Max(1, trunkHeight / 3));

            // Keep the starting point below the very top of the tree.
            startHeight = Math.Min(startHeight, trunkHeight - 2);

            int length =
                MinBranchLength + rng.Next(BranchLengthVariance);

            // Branches generally slope upward slightly.
            int rise = rng.Next(1, 4);

            int startX = x + (TrunkWidth - 1) / 2;
            int startZ = z + (TrunkWidth - 1) / 2;

            int endX = startX + dx * length;
            int endZ = startZ + dz * length;
            int endY = groundY + startHeight + rise;

            PlaceBranch(
                ctx,
                startX,
                groundY + startHeight,
                startZ,
                endX,
                endY,
                endZ);

            // A leaf cluster at each branch tip creates the characteristic
            // broad, branching silhouette of a large oak.
            PlaceLeafCluster(ctx, endX, endY, endZ, 2);
        }
    }

    private void PlaceBranch(
        FeatureContext ctx,
        int startX,
        int startY,
        int startZ,
        int endX,
        int endY,
        int endZ)
    {
        int dx = endX - startX;
        int dy = endY - startY;
        int dz = endZ - startZ;

        int steps = Math.Max(
            Math.Max(Math.Abs(dx), Math.Abs(dy)),
            Math.Abs(dz));

        if (steps == 0)
        {
            ctx.PlaceIfAir(startX, startY, startZ, _logId);
            return;
        }

        // Interpolate between the endpoints to create a continuous
        // staircase-like branch using ordinary cubic blocks.
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;

            int px = startX + (int)Math.Round(dx * t);
            int py = startY + (int)Math.Round(dy * t);
            int pz = startZ + (int)Math.Round(dz * t);

            ctx.PlaceIfAir(px, py, pz, _logId);
        }
    }

    // -----------------------------------------------------------------
    // Main canopy
    // -----------------------------------------------------------------

    private void PlaceCanopy(
        FeatureContext ctx,
        int x,
        int topY,
        int z)
    {
        var rng = ctx.Rng;

        // The upper crown has several overlapping layers.
        // A wider lower crown and a narrower top make the tree look
        // less like a single spherical blob.
        PlaceLeafCluster(ctx, x, topY, z, 3);

        PlaceLeafCluster(ctx, x, topY - 2, z, 3);

        // Add smaller clusters around the crown's perimeter.
        // These connect the foliage to the branch-tip clusters.
        for (int i = 0; i < 4; i++)
        {
            int angle = (i + rng.Next(4)) % 4;

            int dx = 0;
            int dz = 0;

            switch (angle)
            {
                case 0:
                    dx = -2;
                    break;

                case 1:
                    dx = 2;
                    break;

                case 2:
                    dz = -2;
                    break;

                case 3:
                    dz = 2;
                    break;
            }

            int clusterY = topY - rng.Next(0, 3);

            PlaceLeafCluster(
                ctx,
                x + dx,
                clusterY,
                z + dz,
                2);
        }
    }

    // -----------------------------------------------------------------
    // Foliage
    // -----------------------------------------------------------------

    private void PlaceLeafCluster(
        FeatureContext ctx,
        int centerX,
        int centerY,
        int centerZ,
        int radius)
    {
        var rng = ctx.Rng;

        // Build the canopy from stacked square layers with trimmed
        // corners. This gives the foliage a chunky, Minecraft-like look.
        for (int dy = -radius; dy <= radius; dy++)
        {
            int layerRadius = radius;

            // Slightly taper the top and bottom of each cluster.
            if (Math.Abs(dy) == radius)
                layerRadius = Math.Max(1, radius - 1);

            for (int dx = -layerRadius; dx <= layerRadius; dx++)
            for (int dz = -layerRadius; dz <= layerRadius; dz++)
            {
                bool corner =
                    Math.Abs(dx) == layerRadius &&
                    Math.Abs(dz) == layerRadius;

                // Trim corners randomly to avoid a perfectly square crown.
                if (corner && rng.Next(3) != 0)
                    continue;

                // Occasionally remove an outer-edge leaf for a more
                // natural, irregular silhouette.
                bool outerEdge =
                    Math.Abs(dx) == layerRadius ||
                    Math.Abs(dz) == layerRadius;

                if (outerEdge && rng.Next(8) == 0)
                    continue;

                int px = centerX + dx;
                int py = centerY + dy;
                int pz = centerZ + dz;

                if (py < 0 || py >= ctx.Height)
                    continue;

                ctx.PlaceIfAir(px, py, pz, _leavesId);
            }
        }
    }
}