using System;

namespace CuboidFever;

public abstract class Boulder : Feature
{
    private const int SpawnOneIn = 10000;
    private const int MaxRadius = 2;

    protected readonly byte _grassId;
    protected readonly byte _dirtId;
    protected readonly byte _stoneId;
    protected readonly byte _rockId;
    private readonly string _name;
    private readonly int _salt;

    protected Boulder(BlockRegistry blocks, string stoneBlockName, string rockBlockName, string featureName, int salt)
    {
        _grassId = blocks.GetId("cuboidfever:grass");
        _dirtId = blocks.GetId("cuboidfever:dirt");
        _stoneId = blocks.GetId(stoneBlockName);
        _rockId = blocks.GetId(rockBlockName);
        _name = featureName;
        _salt = salt;
    }

    public override string Name => _name;
    public override int Salt => _salt;

    public override void Generate(FeatureContext ctx)
    {
        var rng = ctx.Rng;

        for (int x = MaxRadius; x < ctx.Size - MaxRadius; x++)
        for (int z = MaxRadius; z < ctx.Size - MaxRadius; z++)
        {
            if (rng.Next(SpawnOneIn) != 0)
                continue;

            int groundY = ctx.TerrainSurfaceY(
                x, z, _grassId, _dirtId, _stoneId);

            if (groundY < 0 || ctx.GetBlock(x, groundY, z) != _grassId)
                continue;

            if (groundY + 3 >= ctx.Height)
                continue;

            PlaceBoulder(ctx, x, groundY, z, rng);
        }
    }

    private void PlaceBoulder(
        FeatureContext ctx, int x, int groundY, int z, Random rng)
    {
        TryPavedPlacement(ctx, x, groundY - 1, z, rng);

        for (int dx = -MaxRadius; dx <= MaxRadius; dx++)
        for (int dz = -MaxRadius; dz <= MaxRadius; dz++)
        {
            int distance = dx * dx + dz * dz;

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

            TryPavedPlacement(ctx, px, localGroundY, pz, rng);

            if (distance <= 2 && rng.Next(3) != 0)
                TryPavedPlacement(ctx, px, localGroundY - 1, pz, rng);
        }

        for (int dx = -1; dx <= 1; dx++)
        for (int dz = -1; dz <= 1; dz++)
        {
            if (Math.Abs(dx) == 1 &&
                Math.Abs(dz) == 1 &&
                rng.Next(2) == 0)
                continue;

            TryPavedPlacement(ctx, x + dx, groundY + 1, z + dz, rng);
        }

        TryPavedPlacement(ctx, x, groundY + 2, z, rng);

        if (rng.Next(2) == 0)
        {
            int dx = rng.Next(-1, 2);
            int dz = rng.Next(-1, 2);

            if (dx != 0 || dz != 0)
                TryPavedPlacement(ctx, x + dx, groundY + 2, z + dz, rng);
        }
    }

    private void TryPavedPlacement(FeatureContext ctx, int x, int y, int z, Random rng)
    {
        bool useRockVariant = rng.Next(100) < 8;
        byte targetBlock = useRockVariant ? _rockId : _stoneId;

        if (!ctx.InBounds(x, y, z))
            return;

        if (ctx.ReplaceIfBlock(x, y, z, _grassId, targetBlock))
            return;

        if (ctx.ReplaceIfBlock(x, y, z, _dirtId, targetBlock))
            return;

        if (ctx.GetBlock(x, y, z) == _stoneId || ctx.GetBlock(x, y, z) == _rockId)
            return;

        ctx.PlaceIfAir(x, y, z, targetBlock);
    }
}