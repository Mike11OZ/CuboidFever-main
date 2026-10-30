using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

public class Chunk
{
    public const int Size = 16;

    public readonly int CX, CY, CZ;   // CX and CZ double as the column coordinates
    public readonly BoundingBox Bounds;

    public int Version;               // bumped whenever the mesh needs rebuilding
    public int AppliedVersion = -1;   // version of the mesh currently on the GPU
    public bool Unloaded;

    // ---- Water Wave Configuration ----
    public static float WaterWaveIntensity = 1.0f; // Easily alterable at runtime
    public static float WaterWaveSpeed = 0.8f;      // New: controls how fast the waves animate (try 0.5f to 1.0f)
    private static readonly PerlinNoise WaterNoise = new(new Random(1337));
    public static float WaterFoamThreshold = 0.25f;  // upward displacement (blocks) where foam begins
    public static float WaterFoamSoftness  = 0.4f; // blocks over which it blends to full white

    private readonly GpuMesh _solid = new();
    private readonly WaterMesh _water = new();

    // ---- Block storage ----
    // _layers == null: the whole chunk is one block type (_uniform).
    // Otherwise 16 layers: a null entry means that layer is one block type (_layerFill[y]);
    // a non-null entry is a flat 256-byte array indexed [z * 16 + x].
    private byte _uniform;
    private byte[][] _layers;
    private readonly byte[] _layerFill = new byte[Size];

    public Chunk(int cx, int cy, int cz)
    {
        CX = cx; CY = cy; CZ = cz;
        var min = new Vector3(cx * Size, cy * Size, cz * Size);
        Bounds = new BoundingBox(min, min + new Vector3(Size));
    }

    public bool IsEmpty => _layers == null && _uniform == Block.Air;
    public bool HasMesh => _solid.HasData || _water.HasData;

    // Safe to call from worker threads while the main thread edits: it reads _layers once
    // into a local, and SetBlock publishes new arrays only after filling them.
    public byte GetBlock(int x, int y, int z)
    {
        var layers = _layers;
        if (layers == null) return _uniform;
        var layer = layers[y];
        return layer == null ? _layerFill[y] : layer[z * Size + x];
    }

    public void SetBlock(int x, int y, int z, byte id)
    {
        if (GetBlock(x, y, z) == id) return;

        if (_layers == null)
        {
            Array.Fill(_layerFill, _uniform);
            _layers = new byte[Size][];
        }

        var layer = _layers[y];
        if (layer == null)
        {
            layer = new byte[Size * Size];
            Array.Fill(layer, _layerFill[y]);
            layer[z * Size + x] = id;
            _layers[y] = layer;
        }
        else
        {
            layer[z * Size + x] = id;
        }

        if (TryCollapseLayer(y)) TryCollapseChunk();
    }

    private bool TryCollapseLayer(int y)
    {
        var layer = _layers[y];
        if (layer == null) return false;

        byte v = layer[0];
        for (int i = 1; i < layer.Length; i++)
            if (layer[i] != v) return false;

        _layerFill[y] = v;
        _layers[y] = null;
        return true;
    }

    private void TryCollapseChunk()
    {
        byte v = _layerFill[0];
        for (int y = 0; y < Size; y++)
            if (_layers[y] != null || _layerFill[y] != v) return;

        _uniform = v;
        _layers = null;
    }

    // Copies this chunk's vertical slice out of a full-height column, storing uniform layers compactly
    public void CopyFromColumn(byte[,,] column)
    {
        int oy = CY * Size;
        var layers = new byte[Size][];

        for (int y = 0; y < Size; y++)
        {
            byte first = column[0, oy + y, 0];
            bool uniform = true;
            for (int z = 0; uniform && z < Size; z++)
            for (int x = 0; x < Size; x++)
                if (column[x, oy + y, z] != first) { uniform = false; break; }

            if (uniform)
            {
                _layerFill[y] = first;
            }
            else
            {
                var layer = new byte[Size * Size];
                for (int z = 0; z < Size; z++)
                for (int x = 0; x < Size; x++)
                    layer[z * Size + x] = column[x, oy + y, z];
                layers[y] = layer;
            }
        }
        _layers = layers;
        TryCollapseChunk();
    }

    // =====================  RENDERING  =====================

    // Light level -> brightness. Each level is 80% of the one above (like Minecraft).
    public static readonly float[] Curve = BuildCurve();

    private static float[] BuildCurve()
    {
        var c = new float[16];
        for (int i = 0; i < 16; i++) c[i] = MathF.Pow(0.8f, 15 - i);
        return c;
    }

    // Main thread only: uploads finished mesh data to the GPU
    public void ApplyMesh(GraphicsDevice device, MeshData m)
    {
        _solid.Upload(device, m.SolidV, m.SolidI);
        _water.Upload(device, m.WaterV, m.WaterI, m.WaterF, m.WaterW);
    }

    public void DisposeMesh()
    {
        _solid.Clear();
        _water.Clear();
    }

    public void DrawSolid(GraphicsDevice device) => _solid.Draw(device);
    public void DrawWater(GraphicsDevice device) => _water.Draw(device);

    public void UpdateWater(Vector3 cam, float time)
    {
        Vector3 center = new Vector3(CX * Size + 8f, CY * Size + 8f, CZ * Size + 8f);
        _water.Update(cam, time, center);
    }

    private class GpuMesh
    {
        private VertexBuffer _vb;
        private IndexBuffer _ib;
        private int _tris;

        public bool HasData => _vb != null;

        public void Clear()
        {
            _vb?.Dispose();
            _ib?.Dispose();
            _vb = null; _ib = null;
            _tris = 0;
        }

        public void Upload(GraphicsDevice device, VertexPositionColorTexture[] v, int[] idx)
        {
            Clear();
            if (v == null || idx == null || idx.Length == 0) return;

            _tris = idx.Length / 3;
            _vb = new VertexBuffer(device, typeof(VertexPositionColorTexture), v.Length, BufferUsage.WriteOnly);
            _vb.SetData(v);
            _ib = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, idx.Length, BufferUsage.WriteOnly);
            _ib.SetData(idx);
        }

        public void Draw(GraphicsDevice device)
        {
            if (_vb == null) return;
            device.SetVertexBuffer(_vb);
            device.Indices = _ib;
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _tris);
        }
    }

    // Water mesh whose vertex alpha and wave height are dynamically updated
    private class WaterMesh
    {
        private const float AlphaMin = 0.45f;
        private const float AlphaMax = 0.95f;

        // Distance fade thresholds for smooth distant horizon tapering
        private const float FadeStartDist = 45f;
        private const float FadeEndDist = 75f;
        private const float FadeStartDistSq = FadeStartDist * FadeStartDist;
        private const float FadeEndDistSq = FadeEndDist * FadeEndDist;

        private DynamicVertexBuffer _vb;
        private IndexBuffer _ib;
        private VertexPositionColorTexture[] _verts;
        private VertexPositionColorTexture[] _baseVerts;
        private float[] _shoreWeights; // Per-vertex shoreline constraint from the mesher
        private Color[] _base;
        private byte[] _face;
        private int _tris;
        private float _lastUpdateTime = -1f;

        public bool HasData => _vb != null;

        public void Clear()
        {
            _vb?.Dispose();
            _ib?.Dispose();
            _vb = null; _ib = null;
            _verts = null; _baseVerts = null; _shoreWeights = null; _base = null; _face = null;
            _tris = 0;
        }

        public void Upload(
            GraphicsDevice device,
            VertexPositionColorTexture[] v,
            int[] idx,
            byte[] faces,
            float[] shoreWeights)
        {
            Clear();
            if (v == null || idx == null || idx.Length == 0 || faces == null || shoreWeights == null)
                return;

            if (faces.Length != v.Length || shoreWeights.Length != v.Length)
                throw new ArgumentException("Water vertex metadata must match the water vertex count.");

            _verts = v;
            _face = faces;
            _shoreWeights = shoreWeights;
            _baseVerts = new VertexPositionColorTexture[v.Length];
            Array.Copy(v, _baseVerts, v.Length);

            // Keep the original mesh surface as the baseline. A blanket recession here
            // makes the side faces disagree with the top and is no longer needed.
            _base = new Color[v.Length];
            for (int i = 0; i < v.Length; i++) _base[i] = v[i].Color;

            _tris = idx.Length / 3;
            _vb = new DynamicVertexBuffer(
                device,
                typeof(VertexPositionColorTexture),
                v.Length,
                BufferUsage.WriteOnly);

            _ib = new IndexBuffer(
                device,
                IndexElementSize.ThirtyTwoBits,
                idx.Length,
                BufferUsage.WriteOnly);

            _ib.SetData(idx);
            _vb.SetData(_verts, 0, _verts.Length, SetDataOptions.Discard);
            _lastUpdateTime = float.NegativeInfinity;
        }
        // chunks whose center is farther than this have no wave geometry, only foam
        private const float FarUpdateDist = FadeEndDist + 15f;
        private const float FarUpdateDistSq = FarUpdateDist * FarUpdateDist;

        public void Update(Vector3 cam, float time, Vector3 chunkCenter)
        {
            if (_vb == null) return;

            // Far chunks don't move, only their foam colors shift, so update them less often.
            bool far = Vector3.DistanceSquared(cam, chunkCenter) > FarUpdateDistSq;
            if (time - _lastUpdateTime < (far ? 0.1f : 0.033f)) return;
            _lastUpdateTime = time;

            // Far chunks share a quantized clock so neighbors agree across chunk borders.
            float noiseTime = far ? MathF.Floor(time / 0.1f) * 0.1f : time;

            float intensity = WaterWaveIntensity;
            bool animateWaves = intensity > 0.0001f;

            for (int i = 0; i < _verts.Length; i++)
            {
                Vector3 basePos = _baseVerts[i].Position;
                Vector3 pos = basePos;
                float foam = 0f;

                // The mesher assigns matching wave weights to top-surface vertices and
                // their upper side-edge copies. Lower side edges and bottoms have weight 0.
                float waveWeight = _shoreWeights[i];

                // Far away only top faces need foam, which keeps the noise cost down.
                if (animateWaves && waveWeight > 0f && (!far || _face[i] == 2))
                {
                    float noise = (float)WaterNoise.Noise(
                        basePos.X * 0.35,
                        basePos.Z * 0.35,
                        noiseTime * WaterWaveSpeed);

                    // Full-strength wave height at this point, regardless of distance.
                    // Weight 2 = under an occluding block: it may only move down.
                    float rise = noise * intensity * (waveWeight > 1f ? 1f : waveWeight);
                    if (waveWeight > 1f) rise = MathF.Min(0f, rise);

                    // Foam comes from the unfaded height, so it looks the same at any distance.
                    if (rise > WaterFoamThreshold)
                    {
                        float ft = MathHelper.Clamp(
                            (rise - WaterFoamThreshold) / MathF.Max(WaterFoamSoftness, 0.0001f),
                            0f, 1f);
                        foam = ft * ft * (3f - 2f * ft);   // smoothstep
                    }

                    // Only the vertex movement fades with distance.
                    float distToCamSq = Vector3.DistanceSquared(cam, basePos);
                    if (distToCamSq < FadeEndDistSq)
                    {
                        float fadeFactor = 1f;
                        if (distToCamSq > FadeStartDistSq)
                        {
                            float dist = MathF.Sqrt(distToCamSq);
                            float t = MathHelper.Clamp(
                                (dist - FadeStartDist) / (FadeEndDist - FadeStartDist), 0f, 1f);
                            fadeFactor = 1f - t * t * (3f - 2f * t);
                        }

                        pos = new Vector3(basePos.X, basePos.Y + rise * fadeFactor, basePos.Z);
                    }
                }

                _verts[i].Position = pos;

                // Robust Fresnel alpha calculation
                Vector3 d = cam - _verts[i].Position;
                float distSq = d.LengthSquared();
                if (distSq < 0.0001f) distSq = 0.0001f;

                float along = _face[i] < 2 ? d.X : _face[i] < 4 ? d.Y : d.Z;
                float cos = MathF.Abs(along) / MathF.Sqrt(distSq);
                if (cos > 1.0f) cos = 1.0f;

                float k = 1f - cos;
                k *= k * k;

                float a = AlphaMin + (AlphaMax - AlphaMin) * k;
                if (a < AlphaMin) a = AlphaMin;
                if (a > AlphaMax) a = AlphaMax;

                Color c = _base[i];
                float rr = c.R / 255f, gg = c.G / 255f, bb = c.B / 255f;

                if (foam > 0f)
                {
                    // Keep the baked light level but drop the water tint, so caps stay lit
                    // (dark at night) instead of glowing. Foam is also more opaque than open water.
                    float lit = MathF.Max(rr, MathF.Max(gg, bb));
                    rr += (lit - rr) * foam;
                    gg += (lit - gg) * foam;
                    bb += (lit - bb) * foam;
                    a  += (AlphaMax - a) * foam;
                }

                _verts[i].Color = new Color(rr * a, gg * a, bb * a, a);
            }

            _vb.SetData(_verts, 0, _verts.Length, SetDataOptions.Discard);
        }

        public void Draw(GraphicsDevice device)
        {
            if (_vb == null) return;
            device.SetVertexBuffer(_vb);
            device.Indices = _ib;
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _tris);
        }
    }
}