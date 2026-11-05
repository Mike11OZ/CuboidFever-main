using System;

namespace CuboidFever;

public sealed class FlowerFeature : Feature
{
    private const int SpawnOneIn = 256; // Rarer than grass

    private readonly byte _grassId;
    private readonly byte _airId;
    private readonly byte _flowerId;

    public FlowerFeature(BlockRegistry blocks)
    {
        _grassId = blocks.GetId("cuboidfever:grass");
        _airId = blocks.GetId("cuboidfever:air");
        _flowerId = blocks.GetId("cuboidfever:flower");
    }

    public override string Name => "flower";
    public override int Salt => 0x8053; // Unique salt

    public override void Generate(FeatureContext ctx)
    {
        var rng = ctx.Rng;

        for (int x = 0; x < ctx.Size; x++)
        for (int z = 0; z < ctx.Size; z++)
        {
            if (!ctx.Roll(x, z, Name, SpawnOneIn))
                continue;

            int groundY = ctx.SurfaceY(x, z);

            if (ctx.GetBlock(x, groundY, z) != _grassId)
                continue;

            int plantY = groundY + 1;
            if (ctx.InBounds(x, plantY, z) && ctx.GetBlock(x, plantY, z) == _airId)
            {
                ctx.PlaceIfAir(x, plantY, z, _flowerId);
            }
        }
    }
}