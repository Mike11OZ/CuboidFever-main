namespace CuboidFever;

public sealed class StoneBoulderFeature : Boulder
{
    public StoneBoulderFeature(BlockRegistry blocks) 
        : base(blocks, 
               stoneBlockName: "cuboidfever:stone", 
               rockBlockName: "cuboidfever:rocks", // or a stone_rocks block if you add one later
               featureName: "boulder", 
               salt: 0xB01D3) {}
}