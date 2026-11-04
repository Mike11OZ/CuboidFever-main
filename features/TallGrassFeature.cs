using System;

namespace CuboidFever;

public sealed class TallGrassFeature : Feature
{
    private const int SpawnOneIn = 8; // Adjust density (lower = more frequent)

    private readonly byte _grassId;
    private readonly byte _airId;
    private readonly byte _tallGrassId;

    public TallGrassFeature(BlockRegistry blocks)
    {
        _grassId = blocks.GetId("cuboidfever:grass");
        _airId = blocks.GetId("cuboidfever:air");
        _tallGrassId = blocks.GetId("cuboidfever:tall_grass");
    }

    public override string Name => "tall_grass";
    public override int Salt => 0x61555; // Unique salt

    public override void Generate(FeatureContext ctx)
    {
        var rng = ctx.Rng;

        for (int x = 0; x < ctx.Size; x++)
        for (int z = 0; z < ctx.Size; z++)
        {
            if (rng.Next(SpawnOneIn) != 0)
                continue;

            int groundY = ctx.SurfaceY(x, z);

            // Only spawn on top of grass blocks
            if (ctx.GetBlock(x, groundY, z) != _grassId)
                continue;

            int plantY = groundY + 1;
            if (ctx.InBounds(x, plantY, z) && ctx.GetBlock(x, plantY, z) == _airId)
            {
                ctx.PlaceIfAir(x, plantY, z, _tallGrassId);
            }
        }
    }
}