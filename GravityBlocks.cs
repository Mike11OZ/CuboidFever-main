namespace CuboidFever;

// Blocks that fall when nothing solid is underneath them (sand). Marked with "gravity": true in the block's JSON.
public sealed class GravityBlocks : IBlockTickHandler
{
    public const int FallDelay = 2;

    private readonly World _world;

    public GravityBlocks(World world) => _world = world;

    public int TickDelay => FallDelay;

    public void Tick(int x, int y, int z)
    {
        if (!CanFallInto(x, y - 1, z)) return;

        byte id = _world.GetBlock(x, y, z);

        _world.SetBlock(x, y, z, Block.Air, urgent: false);
        _world.SetBlock(x, y - 1, z, id, urgent: false);
    }

    private bool CanFallInto(int x, int y, int z) => !_world.GetInfoOrWall(x, y, z).Solid;
}