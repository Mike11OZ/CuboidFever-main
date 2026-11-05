namespace CuboidFever;

public sealed class RhyoliteBoulderFeature : Boulder
{
    public RhyoliteBoulderFeature(BlockRegistry blocks) 
        : base(blocks, 
               stoneBlockName: "cuboidfever:rhyolite", 
               rockBlockName: "cuboidfever:rhyolite_rocks", 
               featureName: "rhyolite_boulder", 
               salt: 0x5135A) {}
}