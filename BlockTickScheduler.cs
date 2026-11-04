using System;
using System.Collections.Generic;

namespace CuboidFever;

// Which update rules a block follows. Set per block in its JSON (see BlockRegistry.Bake); None for most blocks.
public enum TickKind : byte
{
    None = 0,
    Water,
    Gravity
}

// The rules for one kind of reactive block: Minecraft's Block.updateTick.
public interface IBlockTickHandler
{
    // Game ticks (1/20 s) between a block being disturbed and its Tick running.
    int TickDelay { get; }

    // Runs on the main thread. The block at (x, y, z) is of the kind this handler was registered for.
    void Tick(int x, int y, int z);
}

// One scheduled-update queue for every block that reacts to its surroundings (water, sand, ...), after Minecraft's
// World.scheduleBlockUpdate / tickUpdates.
public sealed class BlockTickScheduler
{
    public const int TicksPerSecond = 20;

    private const int MaxUpdatesPerTick = 1000;
    private const int MaxCatchUpTicks = 5;

    private readonly World _world;
    private readonly BlockInfo[] _info;
    private readonly IBlockTickHandler[] _handlers = new IBlockTickHandler[Enum.GetValues<TickKind>().Length];

    private readonly PriorityQueue<(int x, int y, int z), (long due, long seq)> _queue = new();
    private readonly HashSet<(int x, int y, int z)> _scheduled = new();
    private long _tick;
    private long _seq;

    public BlockTickScheduler(World world)
    {
        _world = world;
        _info = BlockRegistry.Instance.Info;
    }

    public int PendingUpdates => _queue.Count;

    public void Register(TickKind kind, IBlockTickHandler handler) => _handlers[(int)kind] = handler;

    public void OnBlockChanged(int x, int y, int z)
    {
        Wake(x, y, z);

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
            Wake(nx, ny, nz);
        }
    }

    private void Wake(int x, int y, int z)
    {
        var handler = _handlers[(int)_info[_world.GetBlock(x, y, z)].Tick];
        if (handler == null) return;

        if (!_scheduled.Add((x, y, z))) return;
        _queue.Enqueue((x, y, z), (_tick + handler.TickDelay, _seq++));
    }

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

            _handlers[(int)_info[_world.GetBlock(pos.x, pos.y, pos.z)].Tick]?.Tick(pos.x, pos.y, pos.z);
        }
    }
}