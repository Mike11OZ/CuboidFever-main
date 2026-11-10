
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

public sealed class MeshData
{
    public Chunk Chunk;
    public int Version;
    public VertexPositionColorTextureSky[] SolidV, WaterV;
    public int[] SolidI, WaterI;
    public byte[] WaterF;
    public float[] WaterW; // Per-vertex wave weight (float[] expected by Chunk.cs)
}

public static class ChunkMesher
{
    public const int MaxLeafDepth = 3;
    public static int LeafCullDepth = 3;

    private const int Pad = MaxLeafDepth + 1;
    private const int P = Chunk.Size + 2 * Pad;
    private const int PP = P * P, PCount = P * P * P;

    private static readonly int[] Offsets = { 1, -1, PP, -PP, P, -P };

    private static readonly Vector3[][] FaceCorners =
    {
        new[] { new Vector3(1,0,1), new Vector3(1,1,1), new Vector3(1,1,0), new Vector3(1,0,0) }, // +X
        new[] { new Vector3(0,0,0), new Vector3(0,1,0), new Vector3(0,1,1), new Vector3(0,0,1) }, // -X
        new[] { new Vector3(0,1,1), new Vector3(0,1,0), new Vector3(1,1,0), new Vector3(1,1,1) }, // +Y
        new[] { new Vector3(0,0,0), new Vector3(0,0,1), new Vector3(1,0,1), new Vector3(1,0,0) }, // -Y
        new[] { new Vector3(0,0,1), new Vector3(0,1,1), new Vector3(1,1,1), new Vector3(1,0,1) }, // +Z
        new[] { new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(0,1,0), new Vector3(0,0,0) }, // -Z
    };

    private static readonly Vector2[] CrossUV =
    {
        new(0, 1),
        new(0, 0),
        new(1, 0),
        new(1, 1)
    };

    private static readonly Vector3[][] CrossDiagonalPairs =
    {
        new Vector3[]
        {
            new(0.15f, 0f, 0.15f),
            new(0.15f, 1f, 0.15f),
            new(0.85f, 1f, 0.85f),
            new(0.85f, 0f, 0.85f)
        },
        new Vector3[]
        {
            new(0.15f, 0f, 0.85f),
            new(0.15f, 1f, 0.85f),
            new(0.85f, 1f, 0.15f),
            new(0.85f, 0f, 0.15f)
        }
    };

    public static bool AmbientOcclusion = true;

    private static readonly float[] AoCurve = { 0.1f, 0.25f, 0.5f, 1.0f };
    private static readonly float[] Flat = { 1f, 1f, 1f, 1f };
    private static readonly float[] FullTop = { 1f, 1f, 1f, 1f };

    private static readonly int[,,] AoIdx = BuildAo();

    private static int[,,] BuildAo()
    {
        int[] stride = { 1, PP, P };
        var r = new int[6, 4, 3];

        for (int f = 0; f < 6; f++)
        {
            int na = f / 2;
            int ns = f % 2 == 0 ? 1 : -1;
            int t1 = (na + 1) % 3, t2 = (na + 2) % 3;
            int normalOff = ns * stride[na];

            for (int v = 0; v < 4; v++)
            {
                Vector3 c = FaceCorners[f][v];
                float[] comp = { c.X, c.Y, c.Z };
                int d1 = comp[t1] > 0.5f ? 1 : -1;
                int d2 = comp[t2] > 0.5f ? 1 : -1;

                r[f, v, 0] = normalOff + d1 * stride[t1];
                r[f, v, 1] = normalOff + d2 * stride[t2];
                r[f, v, 2] = normalOff + d1 * stride[t1] + d2 * stride[t2];
            }
        }

        return r;
    }

    private static float AoFactor(byte[] blocks, BlockInfo[] info, int i, int f, int v)
    {
        bool s1 = info[blocks[i + AoIdx[f, v, 0]]].Occludes;
        bool s2 = info[blocks[i + AoIdx[f, v, 1]]].Occludes;
        bool c = info[blocks[i + AoIdx[f, v, 2]]].Occludes;

        int level = (s1 && s2)
            ? 0
            : 3 - ((s1 ? 1 : 0) + (s2 ? 1 : 0) + (c ? 1 : 0));

        return AoCurve[level];
    }

    private static readonly Vector2[] FaceUV =
    {
        new(0, 1), new(0, 0), new(1, 0), new(1, 1)
    };

    private static readonly Vector2[] BottomUV =
    {
        new(0, 0), new(0, 1), new(1, 1), new(1, 0)
    };

    private static readonly float[] FaceShade = { 0.8f, 0.8f, 1.0f, 0.5f, 0.65f, 0.65f };

    private static readonly int[] ColOff = new int[P];
    private static readonly int[] LocalOf = new int[P];

    static ChunkMesher()
    {
        for (int p = 0; p < P; p++)
        {
            ColOff[p] = p < Pad ? -1 : p >= Pad + Chunk.Size ? 1 : 0;
            LocalOf[p] = (p - Pad) & 15;
        }
    }

    private sealed class Builder
    {
        private readonly bool _trackWaterData;
        private readonly float _insetU;
        private readonly float _insetV;

        public readonly List<byte> F;
        public readonly List<float> W;

        public readonly List<VertexPositionColorTextureSky> V = new();
        public readonly List<int> I = new();

        public Builder(bool trackWaterData, float insetU, float insetV)
        {
            _trackWaterData = trackWaterData;
            _insetU = insetU;
            _insetV = insetV;

            if (trackWaterData)
            {
                F = new List<byte>();
                W = new List<float>();
            }
        }

        public void Clear()
        {
            V.Clear();
            I.Clear();

            F?.Clear();
            W?.Clear();
        }

        public void AddFace(
            int f,
            Vector3 pos,
            Color color,
            Color skyColor,
            TextureRegion region,
            float[] ao,
            float[] top)
        {
            int start = V.Count;

            if (_trackWaterData)
            {
                for (int c = 0; c < 4; c++)
                {
                    F!.Add((byte)f);
                    W!.Add(0f);
                }
            }

            float widthU = region.U1 - region.U0;
            float heightV = region.V1 - region.V0;
            Vector2[] uvs = f == 3 ? BottomUV : FaceUV;

            float r = color.R / 255f, g = color.G / 255f;
            float b = color.B / 255f, a = color.A / 255f;
            float sr = skyColor.R / 255f, sg = skyColor.G / 255f;
            float sb = skyColor.B / 255f;

            for (int c = 0; c < 4; c++)
            {
                var uv = new Vector2(
                    region.U0 + _insetU + uvs[c].X * (widthU - 2f * _insetU),
                    region.V0 + _insetV + uvs[c].Y * (heightV - 2f * _insetV));

                float k = ao[c];
                Vector3 vertexPos = FaceCorners[f][c] + pos;

                if (FaceCorners[f][c].Y > 0.5f)
                    vertexPos.Y = pos.Y + top[c];

                V.Add(new VertexPositionColorTextureSky(
                    vertexPos,
                    new Color(r * k, g * k, b * k, a),
                    uv,
                    new Color(sr * k, sg * k, sb * k, 1f)));
            }

            if (ao[0] + ao[2] > ao[1] + ao[3])
            {
                I.Add(start);
                I.Add(start + 1);
                I.Add(start + 2);

                I.Add(start);
                I.Add(start + 2);
                I.Add(start + 3);
            }
            else
            {
                I.Add(start + 1);
                I.Add(start + 2);
                I.Add(start + 3);

                I.Add(start + 1);
                I.Add(start + 3);
                I.Add(start);
            }
        }

        // One face of a box that sits inside a block's cell (doors). 'bmin'/'bmax' are the box corners in block-local
        // coordinates. The texture is stretched over the whole face, except that only the leftmost 'uSpan' of its
        // width is used (the door's 3-pixel edge strip), and 'swapUv' turns the mapping a quarter so a strip
        // lines up with the long way of a top or bottom face.
        public void AddBoxFace(
            int f,
            Vector3 pos,
            Vector3 bmin,
            Vector3 bmax,
            Color color,
            Color skyColor,
            TextureRegion region,
            float uSpan,
            bool swapUv,
            bool mirrorU = false)
        {
            int start = V.Count;

            if (_trackWaterData)
            {
                for (int c = 0; c < 4; c++)
                {
                    F!.Add((byte)f);
                    W!.Add(0f);
                }
            }

            float widthU = (region.U1 - region.U0) * uSpan;
            float heightV = region.V1 - region.V0;
            Vector2[] uvs = f == 3 ? BottomUV : FaceUV;
            Vector3 size = bmax - bmin;

            float r = color.R / 255f, g = color.G / 255f;
            float b = color.B / 255f, a = color.A / 255f;
            float sr = skyColor.R / 255f, sg = skyColor.G / 255f;
            float sb = skyColor.B / 255f;

            for (int c = 0; c < 4; c++)
            {
                Vector3 corner = FaceCorners[f][c];
                Vector2 uvc = swapUv ? new Vector2(uvs[c].Y, uvs[c].X) : uvs[c];
                if (mirrorU) uvc.X = 1f - uvc.X;

                var uv = new Vector2(
                    region.U0 + _insetU + uvc.X * (widthU - 2f * _insetU),
                    region.V0 + _insetV + uvc.Y * (heightV - 2f * _insetV));

                var vertexPos = pos + bmin + new Vector3(corner.X * size.X, corner.Y * size.Y, corner.Z * size.Z);

                V.Add(new VertexPositionColorTextureSky(
                    vertexPos,
                    new Color(r, g, b, a),
                    uv,
                    new Color(sr, sg, sb, 1f)));
            }

            I.Add(start + 1);
            I.Add(start + 2);
            I.Add(start + 3);

            I.Add(start + 1);
            I.Add(start + 3);
            I.Add(start);
        }

        public void AddCross(
            Vector3 pos,
            Color color,
            Color skyColor,
            TextureRegion region)
        {
            float widthU = region.U1 - region.U0;
            float heightV = region.V1 - region.V0;

            float r = color.R / 255f, g = color.G / 255f;
            float b = color.B / 255f, a = color.A / 255f;
            float sr = skyColor.R / 255f, sg = skyColor.G / 255f;
            float sb = skyColor.B / 255f;

            foreach (var quad in CrossDiagonalPairs)
            {
                int start = V.Count;

                if (_trackWaterData)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        F!.Add(0);
                        W!.Add(0f);
                    }
                }

                for (int c = 0; c < 4; c++)
                {
                    var uv = new Vector2(
                        region.U0 + _insetU + CrossUV[c].X * (widthU - 2f * _insetU),
                        region.V0 + _insetV + CrossUV[c].Y * (heightV - 2f * _insetV));

                    V.Add(new VertexPositionColorTextureSky(
                        quad[c] + pos,
                        new Color(r, g, b, a),
                        uv,
                        new Color(sr, sg, sb, 1f)));
                }

                I.Add(start);
                I.Add(start + 1);
                I.Add(start + 2);
                I.Add(start);
                I.Add(start + 2);
                I.Add(start + 3);

                int startRev = V.Count;

                if (_trackWaterData)
                {
                    for (int c = 0; c < 4; c++)
                    {
                        F!.Add(0);
                        W!.Add(0f);
                    }
                }

                for (int c = 3; c >= 0; c--)
                {
                    var uv = new Vector2(
                        region.U0 + _insetU + CrossUV[c].X * (widthU - 2f * _insetU),
                        region.V0 + _insetV + CrossUV[c].Y * (heightV - 2f * _insetV));

                    V.Add(new VertexPositionColorTextureSky(
                        quad[c] + pos,
                        new Color(r, g, b, a),
                        uv,
                        new Color(sr, sg, sb, 1f)));
                }

                I.Add(startRev);
                I.Add(startRev + 1);
                I.Add(startRev + 2);
                I.Add(startRev);
                I.Add(startRev + 2);
                I.Add(startRev + 3);
            }
        }
    }

    private sealed class Buffers
    {
        public readonly byte[] Blocks = new byte[PCount];

        public readonly byte[][] Light =
        {
            new byte[PCount],
            new byte[PCount],
            new byte[PCount],
            new byte[PCount]
        };

        public readonly Builder Solid;
        public readonly Builder Water;

        public readonly Vector3[] GrassTint = new Vector3[Chunk.Size * Chunk.Size];
        public readonly Vector3[] FoliageTint = new Vector3[Chunk.Size * Chunk.Size];

        public readonly float[] AO = new float[4];
        public readonly float[] CornerHeights = new float[4];
        public readonly float[] WaterTop = new float[4];

        public readonly byte StoneId;

        public Buffers(BlockRegistry registry)
        {
            Solid = new Builder(
                trackWaterData: false,
                registry.InsetU,
                registry.InsetV);

            Water = new Builder(
                trackWaterData: true,
                registry.InsetU,
                registry.InsetV);

            StoneId = registry.GetId("cuboidfever:stone");
        }
    }

    [ThreadStatic] private static Buffers _buf;

    private static bool ShouldDrawFace(in BlockInfo self, in BlockInfo nb)
    {
        if (nb.IsAir) return true;
        if (self.IsWater) return false;
        if (self.IsGlass && nb.IsGlass) return false;
        if (self.IsLeaves && nb.IsLeaves) return false;
        if (self.IsGlass) return nb.IsWater || nb.IsLeaves;

        return nb.Transparent;
    }

    private static bool LeafFaceVisible(
        byte[] blocks,
        BlockInfo[] info,
        int neighborIndex,
        int step,
        int depth)
    {
        if (depth == 0) return true;
        if (depth == 1) return false;

        int idx = neighborIndex;

        for (int d = 2; d <= depth + 1; d++)
        {
            idx += step;

            ref readonly BlockInfo bi = ref info[blocks[idx]];

            if (!bi.IsLeaves)
                return true;
        }

        return false;
    }

    private static bool WaterFaceVisible(int f, in BlockInfo nb) =>
        !nb.IsWater && (f == 2 || !nb.Occludes);

    private static float FluidCornerHeight(
        byte[] blocks,
        BlockInfo[] info,
        int i,
        int cx,
        int cz)
    {
        float total = 0f;
        int count = 0;

        for (int k = 0; k < 4; k++)
        {
            int j = i + (cx - (k & 1)) + (cz - (k >> 1)) * P;

            if (info[blocks[j + PP]].IsWater)
                return 1f;

            ref readonly BlockInfo nb = ref info[blocks[j]];

            if (nb.IsWater)
            {
                float air = WaterSurface.PercentAir(nb.FluidLevel);

                if (nb.FluidLevel == BlockRegistry.WaterSourceLevel ||
                    nb.FluidLevel >= BlockRegistry.WaterFallingLevel)
                {
                    total += air * 10f;
                    count += 10;
                }

                total += air;
                count++;
            }
            else if (!nb.Solid)
            {
                total += 1f;
                count++;
            }
        }

        if (count == 0)
            return 1f;

        return 1f - total / count;
    }

    private static int BrighterCell(byte[][] light, int waterCell, int airCell)
    {
        int w = light[0][waterCell] + light[1][waterCell] +
                light[2][waterCell] + light[3][waterCell];

        int a = light[0][airCell] + light[1][airCell] +
                light[2][airCell] + light[3][airCell];

        return w > a ? waterCell : airCell;
    }

    public static MeshData Build(Chunk chunk, Column[] cols, int version, ClimateMap climate)
    {
        bool ambientOcclusion = AmbientOcclusion;
        int leafCullDepth = Math.Clamp(LeafCullDepth, 0, MaxLeafDepth);

        var result = new MeshData { Chunk = chunk, Version = version };

        if (chunk.IsEmpty)
            return result;

        var registry = BlockRegistry.Instance;
        var b = _buf ??= new Buffers(registry);

        Snapshot(chunk, cols, b, registry);

        b.Solid.Clear();
        b.Water.Clear();

        int ox = chunk.CX * Chunk.Size;
        int oy = chunk.CY * Chunk.Size;
        int oz = chunk.CZ * Chunk.Size;

        var info = registry.Info;

        float[] ao = b.AO;
        float[] cornerHeights = b.CornerHeights;
        float[] waterTop = b.WaterTop;

        bool tintsReady = false;

        for (int y = 0; y < Chunk.Size; y++)
        for (int z = 0; z < Chunk.Size; z++)
        for (int x = 0; x < Chunk.Size; x++)
        {
            int i = ((y + Pad) * P + (z + Pad)) * P + (x + Pad);

            byte id = b.Blocks[i];
            ref readonly BlockInfo self = ref info[id];

            if (self.IsAir)
                continue;

            Builder builder = self.IsWater ? b.Water : b.Solid;

            var pos = new Vector3(
                ox + x,
                oy + y,
                oz + z);

            Vector3 tintMul = Vector3.One;

            if (self.BiomeTint != BiomeTintKind.None)
            {
                if (!tintsReady)
                {
                    FillTints(climate, ox, oz, b);
                    tintsReady = true;
                }

                int ci = z * Chunk.Size + x;

                tintMul = self.BiomeTint == BiomeTintKind.Grass
                    ? b.GrassTint[ci]
                    : b.FoliageTint[ci];
            }

            if (self.IsCross)
            {
                LightColors(
                    in self,
                    2,
                    i,
                    b.Light,
                    tintMul,
                    out var crossColor,
                    out var crossSkyColor);

                builder.AddCross(
                    pos,
                    crossColor,
                    crossSkyColor,
                    registry.FastRegion(id, 2));

                continue;
            }

            if (self.IsDoor)
            {
                MeshDoor(b, in self, id, i, pos, tintMul, registry);
                continue;
            }

            bool cornersReady = false;

            for (int f = 0; f < 6; f++)
            {
                int n = i + Offsets[f];
                byte nbId = b.Blocks[n];

                bool draw = self.IsWater
                    ? WaterFaceVisible(f, in info[nbId])
                    : self.IsLeaves && info[nbId].IsLeaves
                        ? LeafFaceVisible(
                            b.Blocks,
                            info,
                            n,
                            Offsets[f],
                            leafCullDepth)
                        : ShouldDrawFace(in self, in info[nbId]);

                if (!draw)
                    continue;

                float[] faceAo = Flat;

                if (ambientOcclusion && !self.IsWater && !self.Emissive)
                {
                    for (int v = 0; v < 4; v++)
                        ao[v] = AoFactor(b.Blocks, info, i, f, v);

                    faceAo = ao;
                }

                float[] top = FullTop;

                if (self.IsWater)
                {
                    if (!cornersReady)
                    {
                        for (int cz = 0; cz < 2; cz++)
                        for (int cx = 0; cx < 2; cx++)
                        {
                            cornerHeights[cx + 2 * cz] =
                                FluidCornerHeight(b.Blocks, info, i, cx, cz);
                        }

                        cornersReady = true;
                    }

                    for (int v = 0; v < 4; v++)
                    {
                        Vector3 corner = FaceCorners[f][v];

                        waterTop[v] =
                            cornerHeights[(int)corner.X + 2 * (int)corner.Z];
                    }

                    top = waterTop;
                }

                int lightSample = !self.IsWater
                    ? n
                    : f == 2 && info[nbId].Occludes
                        ? i
                        : BrighterCell(b.Light, i, n);

                Vector3 faceTint =
                    self.BiomeTint == BiomeTintKind.Grass && f == 3
                        ? Vector3.One
                        : tintMul;

                LightColors(
                    in self,
                    f,
                    lightSample,
                    b.Light,
                    faceTint,
                    out var color,
                    out var skyColor);

                builder.AddFace(
                    f,
                    pos,
                    color,
                    skyColor,
                    registry.FastRegion(id, f),
                    faceAo,
                    top);
            }
        }

        if (b.Solid.I.Count > 0)
        {
            result.SolidV = b.Solid.V.ToArray();
            result.SolidI = b.Solid.I.ToArray();
        }

        if (b.Water.I.Count > 0)
        {
            result.WaterV = b.Water.V.ToArray();
            result.WaterI = b.Water.I.ToArray();
            result.WaterF = b.Water.F!.ToArray();
            result.WaterW = b.Water.W!.ToArray();
        }

        return result;
    }

    // A door is a thin slab (BlockShapes.DoorBox). All six faces are always drawn: the slab never hides a neighbour,
    // and you can see both sides of it. The wide faces use the block's main texture, the top and bottom its "cap"
    // texture and the two end faces its "edge" texture; all come from the state, so the halves can differ.
    private static void MeshDoor(
        Buffers b,
        in BlockInfo self,
        byte id,
        int i,
        Vector3 pos,
        Vector3 tintMul,
        BlockRegistry registry)
    {
        BlockShapes.DoorBox(in self, out Vector3 bmin, out Vector3 bmax);

        bool thinInX = bmax.X - bmin.X < 0.5f;
        int thinAxis = thinInX ? 0 : 2;

        TextureRegion front = registry.FastRegion(id, 0);
        TextureRegion edge = registry.EdgeRegion(id);
        TextureRegion cap = registry.CapRegion(id);

        for (int f = 0; f < 6; f++)
        {
            int axis = f / 2;
            bool wide = axis == thinAxis;

            // The brighter of the door's own cell and the cell the face looks into
            int lightSample = BrighterCell(b.Light, i, i + Offsets[f]);

            LightColors(in self, f, lightSample, b.Light, tintMul, out var color, out var skyColor);

            b.Solid.AddBoxFace(
                f,
                pos,
                bmin,
                bmax,
                color,
                skyColor,
                wide ? front : axis == 1 ? cap : edge,
                wide ? 1f : BlockShapes.DoorThickness,
                swapUv: axis == 1 && !thinInX,
                // Hinge on the right: mirror the front so the handle stays on the free edge and pairs match
                mirrorU: wide && self.DoorHingeRight);
        }
    }

    private static void FillTints(
        ClimateMap climate,
        int ox,
        int oz,
        Buffers b)
    {
        for (int z = 0; z < Chunk.Size; z++)
        for (int x = 0; x < Chunk.Size; x++)
        {
            climate.GetTints(
                ox + x,
                oz + z,
                out var grass,
                out var foliage);

            b.GrassTint[z * Chunk.Size + x] = grass;
            b.FoliageTint[z * Chunk.Size + x] = foliage;
        }
    }

    private static void Snapshot(
        Chunk c,
        Column[] cols,
        Buffers b,
        BlockRegistry registry)
    {
        const byte air = Block.Air;

        byte stone = b.StoneId;
        int baseY = c.CY * Chunk.Size - Pad;

        for (int py = 0; py < P; py++)
        {
            int wy = baseY + py;
            bool outside = wy < 0 || wy >= World.Height;

            for (int pz = 0; pz < P; pz++)
            {
                int rowStart = (ColOff[pz] + 1) * 3;
                int lz = LocalOf[pz];

                for (int px = 0; px < P; px++)
                {
                    int i = (py * P + pz) * P + px;

                    if (outside)
                    {
                        b.Blocks[i] = air;
                        b.Light[0][i] = (byte)(wy < 0 ? 0 : 15);
                        b.Light[1][i] = 0;
                        b.Light[2][i] = 0;
                        b.Light[3][i] = 0;

                        continue;
                    }

                    var col = cols[rowStart + ColOff[px] + 1];

                    if (col == null)
                    {
                        b.Blocks[i] = stone;

                        b.Light[0][i] = 0;
                        b.Light[1][i] = 0;
                        b.Light[2][i] = 0;
                        b.Light[3][i] = 0;

                        continue;
                    }

                    int lx = LocalOf[px];

                    b.Blocks[i] = col.GetBlock(lx, wy, lz);

                    int li = Column.LIdx(lx, wy, lz);

                    bool lit =
                        py >= Pad - 1 && py <= Pad + Chunk.Size &&
                        pz >= Pad - 1 && pz <= Pad + Chunk.Size &&
                        px >= Pad - 1 && px <= Pad + Chunk.Size;

                    if (lit)
                    {
                        b.Light[0][i] = col.Light[0][li];
                        b.Light[1][i] = col.Light[1][li];
                        b.Light[2][i] = col.Light[2][li];
                        b.Light[3][i] = col.Light[3][li];
                    }
                    else
                    {
                        b.Light[0][i] = 0;
                        b.Light[1][i] = 0;
                        b.Light[2][i] = 0;
                        b.Light[3][i] = 0;
                    }
                }
            }
        }
    }

    private static void LightColors(
        in BlockInfo self,
        int face,
        int n,
        byte[][] light,
        Vector3 tint,
        out Color block,
        out Color sky)
    {
        if (self.Emissive)
        {
            block = new Color(
                self.TintR,
                self.TintG,
                self.TintB,
                1f);

            sky = new Color(0f, 0f, 0f, 1f);

            return;
        }

        float shade = FaceShade[face];
        float s = Chunk.Curve[light[0][n]];

        block = new Color(
            Chunk.Curve[light[1][n]] * shade * self.TintR * tint.X,
            Chunk.Curve[light[2][n]] * shade * self.TintG * tint.Y,
            Chunk.Curve[light[3][n]] * shade * self.TintB * tint.Z,
            1f);

        sky = new Color(
            s * shade * self.TintR * tint.X,
            s * shade * self.TintG * tint.Y,
            s * shade * self.TintB * tint.Z,
            1f);
    }
}
