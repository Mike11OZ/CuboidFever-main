using System;
using System.Collections.Generic;

namespace CuboidFever;

// Water flow, ported from Minecraft's BlockFlowing.updateTick (the Beta-era rules).
//
// Minecraft keeps a water block's flow level in block metadata. This game has no metadata, so each level is its
// own block (see BlockRegistry.WaterByLevel):
//     0 = source      1-7 = flowing (1 is thickest, 7 the thinnest edge)      8 = falling
// Minecraft's falling values 8-15 all behave the same here, so they collapse into one block.
//
// Water is not ticked all the time. A water block gets a scheduled update when it is placed, changes, or a
// neighbour changes (World.SetBlock calls OnBlockChanged), and the update runs WaterTickRate game ticks later
// (5 ticks of 1/20 s = 0.25 s, same as Minecraft). An update that changes nothing schedules nothing, so a
// settled lake costs nothing until something disturbs it.
//
// Everything here runs on the main thread.
public sealed class FluidSimulator
{
    public const int TicksPerSecond = 20;
    public const int WaterTickRate = 5;

    private const int MaxUpdatesPerTick = 1000;  // Minecraft's cap; leftovers run on the next tick
    private const int MaxCatchUpTicks = 5;       // after a long frame, skip ahead instead of running a burst

    private const int Falling = BlockRegistry.WaterFallingLevel;

    // Direction index -> offset. Same order as the Java code: 0 = -X, 1 = +X, 2 = -Z, 3 = +Z.
    private static readonly int[] DirX = { -1, 1, 0, 0 };
    private static readonly int[] DirZ = { 0, 0, -1, 1 };
    private static readonly int[] Opposite = { 1, 0, 3, 2 };

    // Stand-in for anything outside the loaded world: solid, so water neither flows into it nor falls out of the world.
    private static readonly BlockInfo Wall = new() { Solid = true, Occludes = true, FluidLevel = -1 };

    private readonly World _world;
    private readonly BlockInfo[] _info;
    private readonly byte[] _waterByLevel;

    private readonly PriorityQueue<(int x, int y, int z), (long due, long seq)> _queue = new();
    private readonly HashSet<(int x, int y, int z)> _scheduled = new();
    private long _tick;
    private long _seq;

    // Scratch state for UpdateTick (it is not re-entrant, and only the main thread uses it)
    private int _adjacentSources;
    private readonly int[] _flowCost = new int[4];
    private readonly bool[] _optimal = new bool[4];

    public FluidSimulator(World world)
    {
        _world = world;
        _info = BlockRegistry.Instance.Info;
        _waterByLevel = BlockRegistry.Instance.WaterByLevel;
    }

    // Water updates waiting to run (for the debug overlay)
    public int PendingUpdates => _queue.Count;

    // =====================  SCHEDULING  =====================

    // Call after any block changes (World.SetBlock does). Wakes the block itself if it is water, and every water
    // neighbour, which is Minecraft's "neighbour changed" notification for fluids.
    public void OnBlockChanged(int x, int y, int z)
    {
        if (_info[_world.GetBlock(x, y, z)].IsWater) Schedule(x, y, z);

        for (int i = 0; i < 6; i++)
        {
            int nx = x, ny = y, nz = z;
            switch (i)
            {
                case 0: nx++; break;
                case 1: nx--; break;
                case 2: ny++; break;
                case 3: ny--; break;
                case 4: nz++; break;
                default: nz--; break;
            }
            if (ny < 0 || ny >= World.Height) continue;
            if (_info[_world.GetBlock(nx, ny, nz)].IsWater) Schedule(nx, ny, nz);
        }
    }

    private void Schedule(int x, int y, int z)
    {
        if (!_scheduled.Add((x, y, z))) return; // already waiting
        _queue.Enqueue((x, y, z), (_tick + WaterTickRate, _seq++));
    }

    // Call once per frame with the total game time in seconds. Runs however many 1/20 s ticks have passed.
    public void Advance(float totalSeconds)
    {
        long target = (long)(totalSeconds * TicksPerSecond);
        if (target - _tick > MaxCatchUpTicks) _tick = target - MaxCatchUpTicks;

        while (_tick < target)
        {
            _tick++;
            RunDueUpdates();
        }
    }

    private void RunDueUpdates()
    {
        int budget = MaxUpdatesPerTick;

        while (budget > 0 && _queue.TryPeek(out var pos, out var priority) && priority.due <= _tick)
        {
            _queue.Dequeue();
            _scheduled.Remove(pos);
            budget--;

            if (!_world.IsColumnLoaded(pos.x, pos.z)) continue;
            if (_info[_world.GetBlock(pos.x, pos.y, pos.z)].IsWater)
                UpdateTick(pos.x, pos.y, pos.z);
        }
    }

    // =====================  BLOCK QUERIES  =====================

    private BlockInfo InfoAt(int x, int y, int z)
    {
        if (y < 0 || !_world.IsColumnLoaded(x, z)) return Wall;
        return _info[_world.GetBlock(x, y, z)];
    }

    // BlockFluid.getFlowDecay: this block's water level, or -1 if it is not water.
    private int FlowDecay(int x, int y, int z)
    {
        var info = InfoAt(x, y, z);
        return info.IsWater ? info.FluidLevel : -1;
    }

    // BlockFlowing.blockBlocksFlow: solid blocks stop water.
    private bool BlocksFlow(int x, int y, int z) => InfoAt(x, y, z).Solid;

    // BlockFlowing.liquidCanDisplaceBlock: water can replace anything that is neither water nor solid (i.e. air).
    private bool CanDisplace(int x, int y, int z)
    {
        var info = InfoAt(x, y, z);
        return !info.IsWater && !info.Solid;
    }

    // The neighbour test used when working out where to spread: not solid, and not a source block.
    private bool CanFlowTo(int x, int y, int z)
    {
        var info = InfoAt(x, y, z);
        return !info.Solid && !(info.IsWater && info.FluidLevel == 0);
    }

    private void SetWater(int x, int y, int z, int level) =>
        _world.SetBlock(x, y, z, _waterByLevel[level], urgent: false);

    // =====================  BlockFlowing.updateTick  =====================

    private void UpdateTick(int x, int y, int z)
    {
        int level = FlowDecay(x, y, z);
        if (level < 0) return;

        const int step = 1; // lava would use 2 outside the Nether

        // Source blocks (level 0) never change on their own. Everything else re-derives its level from its surroundings.
        if (level > 0)
        {
            _adjacentSources = 0;
            int smallest = -100;
            smallest = SmallestFlowDecay(x - 1, y, z, smallest);
            smallest = SmallestFlowDecay(x + 1, y, z, smallest);
            smallest = SmallestFlowDecay(x, y, z - 1, smallest);
            smallest = SmallestFlowDecay(x, y, z + 1, smallest);

            // One step thinner than the thickest horizontal neighbour; no feeder means it dries up.
            int newLevel = smallest + step;
            if (newLevel >= 8 || smallest < 0) newLevel = -1;

            // Water directly above makes this block falling water.
            if (FlowDecay(x, y + 1, z) >= 0) newLevel = Falling;

            // Two or more adjacent sources over a solid block turn this block into a source (the "infinite water" rule).
            // Minecraft's second condition (water below and this block already a source) can never be true at this
            // point, so only the solid-floor case exists.
            if (_adjacentSources >= 2 && BlocksFlow(x, y - 1, z)) newLevel = 0;

            if (newLevel != level)
            {
                level = newLevel;
                if (newLevel < 0)
                {
                    _world.SetBlock(x, y, z, Block.Air, urgent: false);
                    return; // the block is gone; nothing left to spread from here
                }

                // SetBlock wakes this block and its neighbours again, so they re-check against the new level.
                SetWater(x, y, z, newLevel);
            }
        }

        if (CanDisplace(x, y - 1, z))
        {
            // Open space below: water falls.
            SetWater(x, y - 1, z, Falling);
        }
        else if (level == 0 || BlocksFlow(x, y - 1, z))
        {
            // Spread sideways. Sources always do; flowing water only once it has something solid under it.
            var optimal = OptimalFlowDirections(x, y, z);

            int spread = level >= Falling ? 1 : level + step;
            if (spread >= 8) return; // too thin to go any further

            for (int d = 0; d < 4; d++)
                if (optimal[d]) FlowInto(x + DirX[d], y, z + DirZ[d], spread);
        }
    }

    // BlockFlowing.getSmallestFlowDecay: thinner-numbered (= thicker) of the current best and this neighbour.
    // Also counts neighbouring source blocks.
    private int SmallestFlowDecay(int x, int y, int z, int current)
    {
        int d = FlowDecay(x, y, z);
        if (d < 0) return current;

        if (d == 0) _adjacentSources++;
        if (d >= Falling) d = 0; // falling water feeds like a source

        return current >= 0 && d >= current ? current : d;
    }

    // BlockFlowing.flowIntoBlock
    private void FlowInto(int x, int y, int z, int level)
    {
        if (CanDisplace(x, y, z)) SetWater(x, y, z, level);
    }

    // BlockFlowing.getOptimalFlowDirections: water heads for the nearest drop (within 4 blocks). If there is none,
    // every direction ties and it spreads everywhere.
    private bool[] OptimalFlowDirections(int x, int y, int z)
    {
        for (int d = 0; d < 4; d++)
        {
            _flowCost[d] = 1000;

            int nx = x + DirX[d], nz = z + DirZ[d];
            if (!CanFlowTo(nx, y, nz)) continue;

            // 0 = there is a drop right there
            _flowCost[d] = !BlocksFlow(nx, y - 1, nz) ? 0 : FlowCostFrom(nx, y, nz, 1, d);
        }

        int lowest = _flowCost[0];
        for (int d = 1; d < 4; d++)
            if (_flowCost[d] < lowest) lowest = _flowCost[d];

        for (int d = 0; d < 4; d++)
            _optimal[d] = _flowCost[d] == lowest;

        return _optimal;
    }

    // BlockFlowing.calculateFlowCost: how many steps to the nearest drop, searching up to 4 deep and never
    // stepping straight back the way it came.
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
