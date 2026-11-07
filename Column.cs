using System;
using System.Collections.Generic;

namespace CuboidFever;

// A 16 x 256 x 16 stack of chunks plus its light data.
public class Column
{
    public const int Size = Chunk.Size;
    public const int CellCount = Size * World.Height * Size;

    public readonly int CX, CZ;
    public readonly Chunk[] Chunks = new Chunk[World.ChunksY];

    // Channels: 0 = sky, 1 = red, 2 = green, 3 = blue. Each cell holds 0-15.
    public readonly byte[][] Light = new byte[4][];

    public bool Active;    // true once all 8 neighbor columns are loaded (so it can be meshed)
    public bool Modified;  // player edited it; with no WorldSave attached to the World, modified columns are never unloaded
    public bool NeedsSave; // edited since it was last written to the save (or ever, if it never was)

    // Set on a worker thread for a column loaded from disk, and used once by World when the column arrives:
    // blocks (flowing water, floating sand) that must be scheduled to act again. World coordinates.
    public List<(int x, int y, int z)> WakeOnLoad;

    public Column(int cx, int cz) { CX = cx; CZ = cz; }

    public static int LIdx(int x, int y, int z) => (y * Size + z) * Size + x;

    // Local coordinates: x, z in 0..15, y in 0..Height-1
    public byte GetBlock(int x, int y, int z) => Chunks[y >> 4].GetBlock(x, y & 15, z);
    public bool IsTransparent(int x, int y, int z) =>
        BlockRegistry.Instance.Info[GetBlock(x, y, z)].Transparent;

    private static readonly int[,] N =
    {
        { 1, 0, 0 }, { -1, 0, 0 }, { 0, 1, 0 }, { 0, -1, 0 }, { 0, 0, 1 }, { 0, 0, -1 }
    };

    // Lamp and torch light for a column that was loaded from disk. Light is never saved: sky light comes back
    // from ComputeSkyLight, and this does the same for the colored block light (channels 1-3). Generated terrain
    // has no lamps, so only loaded columns need it; elsewhere that light is set when a block is placed
    // (LightMap.OnBlockChanged). Call it after ComputeSkyLight. Light that crosses the column's borders is
    // handled afterwards by LightMap.Stitch, same as for sky light. Runs on a worker thread.
    public void ComputeBlockLight() =>
        ComputeBlockLight(BlockRegistry.Instance.Info, id =>
        {
            var e = BlockRegistry.Instance.Get(id).Emission;
            return (e.R, e.G, e.B);
        });

    // The same with the block data passed in, so it can be tested without a loaded BlockRegistry.
    public void ComputeBlockLight(BlockInfo[] infos, Func<byte, (int r, int g, int b)> emission)
    {
        for (int ch = 1; ch <= 3; ch++)
            Light[ch] ??= new byte[CellCount];

        var queues = new Queue<(int x, int y, int z)>[4];
        for (int ch = 1; ch <= 3; ch++)
            queues[ch] = new Queue<(int x, int y, int z)>();

        // Every emitter starts at its own brightness
        for (int cy = 0; cy < World.ChunksY; cy++)
        {
            var chunk = Chunks[cy];
            if (chunk.IsEmpty) continue;

            for (int ly = 0; ly < Size; ly++)
            for (int z = 0; z < Size; z++)
            for (int x = 0; x < Size; x++)
            {
                byte id = chunk.GetBlock(x, ly, z);
                if (!infos[id].Emissive) continue;

                int y = cy * Size + ly;
                var (r, g, b) = emission(id);
                int index = LIdx(x, y, z);

                if (r > 0) { Light[1][index] = (byte)r; queues[1].Enqueue((x, y, z)); }
                if (g > 0) { Light[2][index] = (byte)g; queues[2].Enqueue((x, y, z)); }
                if (b > 0) { Light[3][index] = (byte)b; queues[3].Enqueue((x, y, z)); }
            }
        }

        // Then spread outward the way LightMap.Propagate does: one level lost per step, plus the block's
        // own attenuation, and only through transparent blocks
        for (int ch = 1; ch <= 3; ch++)
        {
            var q = queues[ch];
            var light = Light[ch];

            while (q.Count > 0)
            {
                var (x, y, z) = q.Dequeue();
                int l = light[LIdx(x, y, z)];
                if (l <= 0) continue;

                for (int i = 0; i < 6; i++)
                {
                    int nx = x + N[i, 0], ny = y + N[i, 1], nz = z + N[i, 2];
                    if (nx < 0 || nz < 0 || nx >= Size || nz >= Size || ny < 0 || ny >= World.Height) continue;

                    var info = infos[GetBlock(nx, ny, nz)];
                    if (!info.Transparent) continue;

                    int nl = l - 1 - (info.IsAir ? 0 : info.LightAttenuation);
                    if (nl <= 0) continue;

                    int ni = LIdx(nx, ny, nz);
                    if (light[ni] < nl)
                    {
                        light[ni] = (byte)nl;
                        q.Enqueue((nx, ny, nz));
                    }
                }
            }
        }
    }

    // Blocks that must start acting again once this column is loaded from disk: flowing water (anything but a
    // source block) and gravity blocks with nothing solid under them. Water and sand only act when the
    // BlockTickScheduler has them scheduled, and that schedule lives in memory only, so without this a stream
    // that was saved mid-flow would stay frozen. Returns world coordinates. Runs on a worker thread.
    public List<(int x, int y, int z)> FindBlocksToWake() => FindBlocksToWake(BlockRegistry.Instance.Info);

    // The same with the block data passed in, so it can be tested without a loaded BlockRegistry.
    public List<(int x, int y, int z)> FindBlocksToWake(BlockInfo[] infos)
    {
        var result = new List<(int x, int y, int z)>();

        for (int cy = 0; cy < World.ChunksY; cy++)
        {
            var chunk = Chunks[cy];
            if (chunk.IsEmpty) continue;

            for (int ly = 0; ly < Size; ly++)
            for (int z = 0; z < Size; z++)
            for (int x = 0; x < Size; x++)
            {
                var info = infos[chunk.GetBlock(x, ly, z)];
                if (info.Tick == TickKind.None) continue;

                int y = cy * Size + ly;

                bool wake = info.Tick == TickKind.Water
                    ? info.FluidLevel != BlockRegistry.WaterSourceLevel
                    : y > 0 && !infos[GetBlock(x, y - 1, z)].Solid;

                if (wake) result.Add((CX * Size + x, y, CZ * Size + z));
            }
        }

        return result;
    }

    // Sky light for this column alone. Runs on a worker thread. Spreading across
    // column borders is handled later by LightMap.Stitch, once neighbors exist.
    public void ComputeSkyLight()
    {
        for (int c = 0; c < 4; c++) Light[c] = new byte[CellCount];
        var sky = Light[0];
        var top = new int[Size, Size];

        for (int x = 0; x < Size; x++)
        for (int z = 0; z < Size; z++)
        {
            int y = World.Height - 1;
            int light = 15;

            while (y >= 0)
            {
                byte blockId = GetBlock(x, y, z);
                var info = BlockRegistry.Instance.Info[blockId];

                if (!info.Transparent)
                    break;

                sky[LIdx(x, y, z)] = (byte)light;

                // Air and clear glass preserve full-strength direct skylight.
                // Other transparent blocks apply their additional attenuation.
                if (!info.IsAir)
                    light = Math.Max(0, light - info.LightAttenuation);

                y--;
            }

            top[x, z] = y;
        }

        // Seed every lit cell that can still brighten a horizontal neighbor.
        // (The vertical pass alone leaves e.g. 11 sitting next to 13 under tree canopies.)
        var q = new Queue<(int x, int y, int z)>();
        for (int x = 0; x < Size; x++)
        for (int z = 0; z < Size; z++)
        for (int y = 0; y < World.Height; y++)
        {
            int l = sky[LIdx(x, y, z)];
            if (l <= 1) continue;

            for (int i = 0; i < 6; i++)
            {
                if (i == 2 || i == 3) continue; // horizontal only
                int nx = x + N[i, 0], nz = z + N[i, 2];
                if (nx < 0 || nz < 0 || nx >= Size || nz >= Size) continue;
                if (!IsTransparent(nx, y, nz)) continue;

                var info = BlockRegistry.Instance.Info[GetBlock(nx, y, nz)];
                int extra = info.IsAir ? 0 : info.LightAttenuation;
                if (sky[LIdx(nx, y, nz)] < l - 1 - extra) { q.Enqueue((x, y, z)); break; }
            }
        }
                while (q.Count > 0)
        {
            var (x, y, z) = q.Dequeue();
            int l = sky[LIdx(x, y, z)];
            if (l <= 0) continue;

            for (int i = 0; i < 6; i++)
            {
                int nx = x + N[i, 0], ny = y + N[i, 1], nz = z + N[i, 2];
                if (nx < 0 || nz < 0 || nx >= Size || nz >= Size || ny < 0 || ny >= World.Height) continue;
                if (!IsTransparent(nx, ny, nz))
                    continue;

                byte blockId = GetBlock(nx, ny, nz);
                var info = BlockRegistry.Instance.Info[blockId];

                // Normal propagation costs one level.
                // Block attenuation adds an additional cost.
                // Direct skylight at level 15 is preserved through air and clear glass.
                int baseCost = i == 3 && l == 15 ? 0 : 1;
                int extraCost = info.IsAir ? 0 : info.LightAttenuation;

                int nl = l - baseCost - extraCost;
                if (nl <= 0)
                    continue;

                int ni = LIdx(nx, ny, nz);

                if (sky[ni] < nl)
                {
                    sky[ni] = (byte)nl;
                    q.Enqueue((nx, ny, nz));
                }
            }
        }
    }
}