namespace CuboidFever;

// Water flow, ported from Minecraft's BlockFlowing.updateTick (the Beta-era rules).
public sealed class FluidSimulator : IBlockTickHandler
{
    public const int WaterTickRate = 5;

    private const int Falling = BlockRegistry.WaterFallingLevel;

    private static readonly int[] DirX = { -1, 1, 0, 0 };
    private static readonly int[] DirZ = { 0, 0, -1, 1 };
    private static readonly int[] Opposite = { 1, 0, 3, 2 };

    private readonly World _world;
    private readonly byte[] _waterByLevel;

    private int _adjacentSources;
    private readonly int[] _flowCost = new int[4];
    private readonly bool[] _optimal = new bool[4];

    public FluidSimulator(World world)
    {
        _world = world;
        _waterByLevel = BlockRegistry.Instance.WaterByLevel;
    }

    public int TickDelay => WaterTickRate;

    private BlockInfo InfoAt(int x, int y, int z) => _world.GetInfoOrWall(x, y, z);

    private int FlowDecay(int x, int y, int z)
    {
        var info = InfoAt(x, y, z);
        return info.IsWater ? info.FluidLevel : -1;
    }

    private bool BlocksFlow(int x, int y, int z) => InfoAt(x, y, z).Solid;

    private bool CanDisplace(int x, int y, int z)
    {
        var info = InfoAt(x, y, z);
        return !info.IsWater && !info.Solid;
    }

    private bool CanFlowTo(int x, int y, int z)
    {
        var info = InfoAt(x, y, z);
        return !info.Solid && !(info.IsWater && info.FluidLevel == 0);
    }

    private void SetWater(int x, int y, int z, int level) =>
        _world.SetBlock(x, y, z, _waterByLevel[level], urgent: false);

    public void Tick(int x, int y, int z)
    {
        int level = FlowDecay(x, y, z);
        if (level < 0) return;

        const int step = 1;

        if (level > 0)
        {
            _adjacentSources = 0;
            int smallest = -100;
            smallest = SmallestFlowDecay(x - 1, y, z, smallest);
            smallest = SmallestFlowDecay(x + 1, y, z, smallest);
            smallest = SmallestFlowDecay(x, y, z - 1, smallest);
            smallest = SmallestFlowDecay(x, y, z + 1, smallest);

            int newLevel = smallest + step;
            if (newLevel >= 8 || smallest < 0) newLevel = -1;

            if (FlowDecay(x, y + 1, z) >= 0) newLevel = Falling;

            if (_adjacentSources >= 2 && BlocksFlow(x, y - 1, z)) newLevel = 0;

            if (newLevel != level)
            {
                level = newLevel;
                if (newLevel < 0)
                {
                    _world.SetBlock(x, y, z, Block.Air, urgent: false);
                    return;
                }

                SetWater(x, y, z, newLevel);
            }
        }

        if (CanDisplace(x, y - 1, z))
        {
            SetWater(x, y - 1, z, Falling);
        }
        else if (level == 0 || BlocksFlow(x, y - 1, z))
        {
            var optimal = OptimalFlowDirections(x, y, z);

            int spread = level >= Falling ? 1 : level + step;
            if (spread >= 8) return;

            for (int d = 0; d < 4; d++)
                if (optimal[d]) FlowInto(x + DirX[d], y, z + DirZ[d], spread);
        }
    }

    private int SmallestFlowDecay(int x, int y, int z, int current)
    {
        int d = FlowDecay(x, y, z);
        if (d < 0) return current;

        if (d == 0) _adjacentSources++;
        if (d >= Falling) d = 0;

        return current >= 0 && d >= current ? current : d;
    }

    private void FlowInto(int x, int y, int z, int level)
    {
        if (CanDisplace(x, y, z)) SetWater(x, y, z, level);
    }

    private bool[] OptimalFlowDirections(int x, int y, int z)
    {
        for (int d = 0; d < 4; d++)
        {
            _flowCost[d] = 1000;

            int nx = x + DirX[d], nz = z + DirZ[d];
            if (!CanFlowTo(nx, y, nz)) continue;

            _flowCost[d] = !BlocksFlow(nx, y - 1, nz) ? 0 : FlowCostFrom(nx, y, nz, 1, d);
        }

        int lowest = _flowCost[0];
        for (int d = 1; d < 4; d++)
            if (_flowCost[d] < lowest) lowest = _flowCost[d];

        for (int d = 0; d < 4; d++)
            _optimal[d] = _flowCost[d] == lowest;

        return _optimal;
    }

    private int FlowCostFrom(int x, int y, int z, int depth, int fromDir)
    {
        int best = 1000;

        for (int d = 0; d < 4; d++)
        {
            if (d == Opposite[fromDir]) continue;

            int nx = x + DirX[d], nz = z + DirZ[d];
            if (!CanFlowTo(nx, y, nz)) continue;

            if (!BlocksFlow(nx, y - 1, nz)) return depth;

            if (depth < 4)
            {
                int cost = FlowCostFrom(nx, y, nz, depth + 1, d);
                if (cost < best) best = cost;
            }
        }

        return best;
    }
}