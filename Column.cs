using System;
using System.Collections.Generic;

namespace CuboidFever;

// A 16 x 128 x 16 stack of chunks plus its light data.
public class Column
{
    public const int Size = Chunk.Size;
    public const int CellCount = Size * World.Height * Size;

    public readonly int CX, CZ;
    public readonly Chunk[] Chunks = new Chunk[World.ChunksY];

    // Channels: 0 = sky, 1 = red, 2 = green, 3 = blue. Each cell holds 0-15.
    public readonly byte[][] Light = new byte[4][];

    public bool Active;    // true once all 8 neighbor columns are loaded (so it can be meshed)
    public bool Modified;  // player edited it; modified columns are never unloaded (no saving yet)

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