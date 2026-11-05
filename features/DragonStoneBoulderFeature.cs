namespace CuboidFever;

public sealed class DragonStoneBoulderFeature : Boulder
{
    public DragonStoneBoulderFeature(BlockRegistry blocks) 
        : base(blocks, 
               stoneBlockName: "cuboidfever:dragon_stone", 
               rockBlockName: "cuboidfever:dragon_rocks", 
               featureName: "dragon_stone_boulder", 
               salt: 0xD9914) {}
}