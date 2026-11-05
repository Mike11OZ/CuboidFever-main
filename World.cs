using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

public class World : IDisposable
{
    public const int ChunksY = 8;
    public const int Height = ChunksY * Chunk.Size; // 128
    public const int SeaLevel = 64;

    private static readonly int[] RenderDistances = { 8, 12, 16, 20, 24, 32 };
    // Static because render distance is a player setting, not part of one world: it has to be settable from
    // the main menu before any world exists, and carry over into the next world that is created.
    private static int _renderDistanceIndex = 2; // Default 16 is at index 2

    public static int RenderDistanceSetting => RenderDistances[_renderDistanceIndex];

    // Moves the setting to the next distance. Use World.CycleRenderDistance instead when a world exists,
    // since that also applies the change to the running world.
    public static void AdvanceRenderDistanceSetting() =>
        _renderDistanceIndex = (_renderDistanceIndex + 1) % RenderDistances.Length;

    public int LoadRadius = RenderDistances[_renderDistanceIndex];
    private int UnloadRadius => LoadRadius + 2;

    private readonly Dictionary<(int, int), Column> _columns = new();
    private readonly HashSet<(int, int)> _generating = new();
    private readonly HashSet<Chunk> _pending = new();
    private readonly HashSet<Chunk> _urgent = new();
    private readonly HashSet<Chunk> _inFlight = new();
    private readonly List<Chunk> _visible = new();

    private readonly byte _airId;

    private bool _urgentMode;

    private readonly ConcurrentQueue<Column> _generated = new();
    private readonly ConcurrentQueue<MeshData> _meshed = new();

    private readonly ConcurrentQueue<(int cx, int cz, Exception error)>
        _generationFailures = new();

    private readonly TerrainGenerator _gen;
    private readonly CaveCarver _caves;
    private readonly PlacedFeatures _features;
    private readonly WorkerPool _pool;
    private readonly int _maxGenInFlight, _maxMeshInFlight;
    private (int x, int z) _lastCenter = (int.MinValue, 0);

    public readonly LightMap Light;
    private readonly BlockTickScheduler _ticks;
    public int SkyDarkness;

    public World(int seed)
    {
        _airId = BlockRegistry.Instance.GetId("cuboidfever:air");

        _gen = new TerrainGenerator(seed);
        _caves = new CaveCarver(seed, BlockRegistry.Instance);
        _features = new PlacedFeatures(seed, BlockRegistry.Instance, _gen.Climate);
        Light = new LightMap(this);
        _ticks = new BlockTickScheduler(this);
        _ticks.Register(TickKind.Water, new FluidSimulator(this));
        _ticks.Register(TickKind.Gravity, new GravityBlocks(this));
        _pool = new WorkerPool(Math.Clamp(Environment.ProcessorCount - 2, 2, 8));
        _maxGenInFlight = _pool.Count + 2;
        _maxMeshInFlight = _pool.Count * 3;
    }

    // Shuts the world down when the player leaves it (Exit World). Stops the worker threads and frees every
    // chunk's GPU buffers. The world can't be used afterwards; a new game builds a new World.
    public void Dispose()
    {
        // Waits for the jobs already running or queued to finish. The number of in-flight jobs is capped
        // (_maxGenInFlight / _maxMeshInFlight), so this is brief.
        _pool.Dispose();

        foreach (var col in _columns.Values)
        {
            foreach (var chunk in col.Chunks)
            {
                chunk.Unloaded = true;
                chunk.DisposeMesh();
            }
        }

        _columns.Clear();
        _generating.Clear();
        _pending.Clear();
        _urgent.Clear();
        _inFlight.Clear();
        _visible.Clear();
    }

    public void CycleRenderDistance()
    {
        AdvanceRenderDistanceSetting();
        LoadRadius = RenderDistanceSetting;
        _lastCenter = (int.MinValue, 0); // Forces UnloadFar to evaluate distance changes immediately
        Console.WriteLine($"[World] Render distance cycled to: {LoadRadius} chunks");
    }

    public int TotalChunks => _columns.Count * ChunksY;
    public int VisibleChunks => _visible.Count;
    public int PendingWork => _generating.Count + _inFlight.Count + _pending.Count;
    public int PendingBlockUpdates => _ticks.PendingUpdates;

    public Column GetColumn(int cx, int cz) => _columns.TryGetValue((cx, cz), out var c) ? c : null;

    public bool IsColumnLoaded(int wx, int wz) => _columns.ContainsKey((wx >> 4, wz >> 4));

    private Chunk GetChunkAt(int x, int y, int z)
    {
        if ((uint)y >= Height) return null;
        return GetColumn(x >> 4, z >> 4)?.Chunks[y >> 4];
    }

    /// <summary>Biome at a world block column (computed from climate noise, so it works for unloaded terrain too).</summary>
    public Biome GetBiomeAt(int x, int z) => _gen.Climate.GetBiome(x, z);

    /// <summary>Temperature and humidity (0..1) at a world block column.</summary>
    public (double temperature, double humidity) GetClimateAt(int x, int z) => _gen.Climate.Sample(x, z);

    public byte GetBlock(int x, int y, int z)
    {
        if ((uint)y >= Height) return _airId;

        var col = GetColumn(x >> 4, z >> 4);
        return col == null
            ? _airId
            : col.GetBlock(x & 15, y, z & 15);
    }

    public bool IsSolid(int x, int y, int z)
    {
        byte b = GetBlock(x, y, z);
        return BlockRegistry.Instance.Info[b].Solid;
    }

    private static readonly BlockInfo Wall = new() { Solid = true, Occludes = true, FluidLevel = -1 };

    public BlockInfo GetInfoOrWall(int x, int y, int z)
    {
        if (y < 0 || !IsColumnLoaded(x, z)) return Wall;
        return BlockRegistry.Instance.Info[GetBlock(x, y, z)];
    }

    public void RemeshAll()
    {
        foreach (var col in _columns.Values)
        {
            if (!col.Active) continue;
            foreach (var chunk in col.Chunks)
                if (!chunk.IsEmpty) MarkDirty(chunk);
        }
    }

    public void Update(GraphicsDevice device, Vector3 camPos, float time)
    {
        int ccx = (int)MathF.Floor(camPos.X / 16f);
        int ccz = (int)MathF.Floor(camPos.Z / 16f);

        RequestColumns(ccx, ccz);
        DrainGenerated(ccx, ccz);
        DrainGenerationFailures();

        if ((ccx, ccz) != _lastCenter)
        {
            UnloadFar(ccx, ccz);
            _lastCenter = (ccx, ccz);
        }

        _ticks.Advance(time);
        BuildUrgent(device);
        DispatchMeshJobs(device, camPos);
        DrainMeshed(device);
    }

    private readonly List<(int d, int x, int z)> _cand = new();

    private void RequestColumns(int ccx, int ccz)
    {
        int slots = _maxGenInFlight - _generating.Count;
        if (slots <= 0) return;

        _cand.Clear();
        int R = LoadRadius;
        for (int dz = -R; dz <= R; dz++)
        for (int dx = -R; dx <= R; dx++)
        {
            int d = dx * dx + dz * dz;
            if (d > R * R) continue;
            var key = (ccx + dx, ccz + dz);
            if (_columns.ContainsKey(key) || _generating.Contains(key)) continue;
            _cand.Add((d, key.Item1, key.Item2));
        }
        if (_cand.Count == 0) return;

        _cand.Sort((a, b) => a.d.CompareTo(b.d));

        int count = Math.Min(slots, _cand.Count);
        for (int i = 0; i < count; i++)
        {
            var (_, x, z) = _cand[i];
            _generating.Add((x, z));

            try
            {
                _pool.Enqueue(() => GenerateJob(x, z));
            }
            catch
            {
                _generating.Remove((x, z));
                throw;
            }
        }
    }

    private void GenerateJob(int cx, int cz)
    {
        try
        {
            var blocks = new byte[Chunk.Size, Height, Chunk.Size];

            _gen.GenerateColumn(cx, cz, blocks);
            _caves.Carve(cx, cz, blocks);
            _features.Generate(cx, cz, blocks);

            var col = new Column(cx, cz);

            for (int cy = 0; cy < ChunksY; cy++)
            {
                var chunk = new Chunk(cx, cy, cz);
                chunk.CopyFromColumn(blocks);
                col.Chunks[cy] = chunk;
            }

            col.ComputeSkyLight();

            _generated.Enqueue(col);
        }
        catch (Exception e)
        {
            _generationFailures.Enqueue((cx, cz, e));
        }
    }
    private void DrainGenerated(int ccx, int ccz)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < 3.0 && _generated.TryDequeue(out var col))
        {
            _generating.Remove((col.CX, col.CZ));

            int dx = col.CX - ccx, dz = col.CZ - ccz;
            if (dx * dx + dz * dz > UnloadRadius * UnloadRadius) continue;
            if (_columns.ContainsKey((col.CX, col.CZ))) continue;

            _columns[(col.CX, col.CZ)] = col;
            Light.Stitch(col);

            for (int nz = -1; nz <= 1; nz++)
            for (int nx = -1; nx <= 1; nx++)
                TryActivate(GetColumn(col.CX + nx, col.CZ + nz));
        }
    }
    private void DrainGenerationFailures()
    {
        while (_generationFailures.TryDequeue(out var failure))
        {
            _generating.Remove((failure.cx, failure.cz));

            Console.Error.WriteLine(
                $"[World] Failed to generate column " +
                $"({failure.cx}, {failure.cz}): {failure.error}");
        }
    }

    private void TryActivate(Column col)
    {
        if (col == null || col.Active) return;
        for (int dz = -1; dz <= 1; dz++)
        for (int dx = -1; dx <= 1; dx++)
            if ((dx != 0 || dz != 0) && !_columns.ContainsKey((col.CX + dx, col.CZ + dz))) return;

        col.Active = true;
        foreach (var chunk in col.Chunks)
            if (!chunk.IsEmpty) MarkDirty(chunk);
    }

    private readonly List<(int, int)> _toRemove = new();

    private void UnloadFar(int ccx, int ccz)
    {
        _toRemove.Clear();
        int r2 = UnloadRadius * UnloadRadius;

        foreach (var kv in _columns)
        {
            var c = kv.Value;
            int dx = c.CX - ccx, dz = c.CZ - ccz;
            if (dx * dx + dz * dz > r2 && !c.Modified) _toRemove.Add(kv.Key);
        }

        foreach (var key in _toRemove)
        {
            foreach (var chunk in _columns[key].Chunks)
            {
                chunk.Unloaded = true;
                chunk.DisposeMesh();
                _pending.Remove(chunk);
                _urgent.Remove(chunk);
            }
            _columns.Remove(key);
        }
        if (_toRemove.Count > 0) Light.InvalidateCache();
    }

    private Column[] Neighborhood(int cx, int cz)
    {
        var cols = new Column[9];
        for (int dz = -1; dz <= 1; dz++)
        for (int dx = -1; dx <= 1; dx++)
            cols[(dz + 1) * 3 + (dx + 1)] = GetColumn(cx + dx, cz + dz);
        return cols;
    }

    private void BuildUrgent(GraphicsDevice device)
    {
        if (_urgent.Count == 0) return;
        foreach (var c in _urgent)
        {
            if (c.Unloaded) continue;
            var m = ChunkMesher.Build(c, Neighborhood(c.CX, c.CZ), c.Version, _gen.Climate);
            c.ApplyMesh(device, m);
            c.AppliedVersion = m.Version;
        }
        _urgent.Clear();
    }

    private readonly List<(float d, Chunk c)> _ready = new();
    private readonly List<Chunk> _emptyDone = new();

    private void DispatchMeshJobs(GraphicsDevice device, Vector3 camPos)
    {
        if (_pending.Count == 0) return;

        _ready.Clear();
        _emptyDone.Clear();
        foreach (var c in _pending)
        {
            if (_inFlight.Contains(c)) continue;
            if (c.IsEmpty) { _emptyDone.Add(c); continue; }

            var ctr = (c.Bounds.Min + c.Bounds.Max) * 0.5f;
            _ready.Add((Vector3.DistanceSquared(ctr, camPos), c));
        }

        foreach (var c in _emptyDone)
        {
            c.ApplyMesh(device, new MeshData { Chunk = c, Version = c.Version });
            c.AppliedVersion = c.Version;
            _pending.Remove(c);
        }

        int slots = _maxMeshInFlight - _inFlight.Count;
        if (slots <= 0 || _ready.Count == 0) return;

        _ready.Sort((a, b) => a.d.CompareTo(b.d));

        int count = Math.Min(slots, _ready.Count);
        for (int i = 0; i < count; i++)
        {
            var chunk = _ready[i].c;
            var cols = Neighborhood(chunk.CX, chunk.CZ);
            int version = chunk.Version;

            _pending.Remove(chunk);
            _inFlight.Add(chunk);
            _pool.Enqueue(() => _meshed.Enqueue(ChunkMesher.Build(chunk, cols, version, _gen.Climate)));
        }
    }

    private void DrainMeshed(GraphicsDevice device)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalMilliseconds < 4.0 && _meshed.TryDequeue(out var m))
        {
            _inFlight.Remove(m.Chunk);
            if (m.Chunk.Unloaded) continue;

            if (m.Version == m.Chunk.Version &&
                m.Version > m.Chunk.AppliedVersion)
            {
                m.Chunk.ApplyMesh(device, m);
                m.Chunk.AppliedVersion = m.Version;
            }
        }
    }

    private void MarkDirty(Chunk c)
    {
        c.Version++;
        if (_urgentMode)
        {
            _urgent.Add(c);
            _pending.Remove(c);
        }
        else if (GetColumn(c.CX, c.CZ)?.Active == true)
        {
            _pending.Add(c);
        }
    }

    private void MarkDirtyAt(int x, int y, int z)
    {
        var c = GetChunkAt(x, y, z);
        if (c != null) MarkDirty(c);
    }

    public void SetBlock(int x, int y, int z, byte id, bool urgent = true)
    {
        if ((uint)y >= Height) return;
        var col = GetColumn(x >> 4, z >> 4);
        if (col == null) return;

        int lx = x & 15, lz = z & 15;
        var chunk = col.Chunks[y >> 4];
        int ly = y & 15;
        byte old = chunk.GetBlock(lx, ly, lz);
        if (old == id) return;

        chunk.SetBlock(lx, ly, lz, id);
        col.Modified = true;

        _urgentMode = urgent;

        for (int dz = -1; dz <= 1; dz++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            MarkDirtyAt(x + dx, y + dy, z + dz);
        }

        var info = BlockRegistry.Instance.Info;
        if (!(info[old].IsWater && info[id].IsWater)) Light.OnBlockChanged(x, y, z);
        _urgentMode = false;

        _ticks.OnBlockChanged(x, y, z);
    }
    
    public void MarkLightDirty(int x, int y, int z)
    {
        int lx = x & 15, ly = y & 15, lz = z & 15;
        MarkDirtyAt(x, y, z);
        if (lx == 0)  MarkDirtyAt(x - 1, y, z);
        if (lx == 15) MarkDirtyAt(x + 1, y, z);
        if (ly == 0)  MarkDirtyAt(x, y - 1, z);
        if (ly == 15) MarkDirtyAt(x, y + 1, z);
        if (lz == 0)  MarkDirtyAt(x, y, z - 1);
        if (lz == 15) MarkDirtyAt(x, y, z + 1);
    }

    public void UpdateVisibility(BoundingFrustum frustum, Vector3 camPos, float time)
    {
        _visible.Clear();
        float maxDist = (LoadRadius + 1) * Chunk.Size;
        float maxSq = maxDist * maxDist;

        foreach (var col in _columns.Values)
        {
            foreach (var chunk in col.Chunks)
            {
                if (!chunk.HasMesh) continue;

                float dx = (chunk.Bounds.Min.X + 8f) - camPos.X;
                float dz = (chunk.Bounds.Min.Z + 8f) - camPos.Z;
                if (dx * dx + dz * dz > maxSq) continue;

                if (frustum.Intersects(chunk.Bounds)) _visible.Add(chunk);
            }
        }
    }

    public void DrawSolid(GraphicsDevice device)
    {
        foreach (var chunk in _visible) chunk.DrawSolid(device);
    }

    public void DrawWater(GraphicsDevice device)
    {
        foreach (var chunk in _visible) chunk.DrawWater(device);
    }

    public bool Raycast(Vector3 origin, Vector3 dir, float maxDist,
                        out (int x, int y, int z) hit, out (int x, int y, int z) prev)
    {
        var blockInfo = BlockRegistry.Instance.Info;
        int x = (int)MathF.Floor(origin.X);
        int y = (int)MathF.Floor(origin.Y);
        int z = (int)MathF.Floor(origin.Z);
        hit = prev = (x, y, z);

        int stepX = dir.X > 0 ? 1 : -1, stepY = dir.Y > 0 ? 1 : -1, stepZ = dir.Z > 0 ? 1 : -1;

        float tDX = dir.X != 0 ? MathF.Abs(1f / dir.X) : float.PositiveInfinity;
        float tDY = dir.Y != 0 ? MathF.Abs(1f / dir.Y) : float.PositiveInfinity;
        float tDZ = dir.Z != 0 ? MathF.Abs(1f / dir.Z) : float.PositiveInfinity;

        float tMX = dir.X != 0 ? (stepX > 0 ? x + 1 - origin.X : origin.X - x) * tDX : float.PositiveInfinity;
        float tMY = dir.Y != 0 ? (stepY > 0 ? y + 1 - origin.Y : origin.Y - y) * tDY : float.PositiveInfinity;
        float tMZ = dir.Z != 0 ? (stepZ > 0 ? z + 1 - origin.Z : origin.Z - z) * tDZ : float.PositiveInfinity;

        float t = 0;
        while (t <= maxDist)
        {
            byte b = GetBlock(x, y, z);
            if (b != _airId && !blockInfo[b].IsWater)
            {
                hit = (x, y, z);
                return true;
            }
            prev = (x, y, z);

            if (tMX < tMY && tMX < tMZ) { x += stepX; t = tMX; tMX += tDX; }
            else if (tMY < tMZ)         { y += stepY; t = tMY; tMY += tDY; }
            else                        { z += stepZ; t = tMZ; tMZ += tDZ; }
        }
        return false;
    }
}