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
    public static float WaterWaveIntensity = 0.05f; // Easily alterable at runtime
    public static float WaterWaveSpeed = 0.8f;      // New: controls how fast the waves animate (try 0.5f to 1.0f)
    private static readonly PerlinNoise WaterNoise = new(new Random(1337));

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
        _water.Upload(device, m.WaterV, m.WaterI, m.WaterF);
    }

    public void DisposeMesh()
    {
        _solid.Clear();
        _water.Clear();
    }

    public void DrawSolid(GraphicsDevice device) => _solid.Draw(device);
    public void DrawWater(GraphicsDevice device) => _water.Draw(device);
    public void UpdateWater(Vector3 cam, float time) => _water.Update(cam, time);
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

        private DynamicVertexBuffer _vb;
        private IndexBuffer _ib;
        private VertexPositionColorTexture[] _verts;
        private VertexPositionColorTexture[] _baseVerts; // Store unperturbed base vertices
        private Color[] _base;   
        private byte[] _face;    
        private int _tris;

        public bool HasData => _vb != null;

        public void Clear()
        {
            _vb?.Dispose();
            _ib?.Dispose();
            _vb = null; _ib = null;
            _verts = null; _baseVerts = null; _base = null; _face = null;
            _tris = 0;
        }

        public void Upload(GraphicsDevice device, VertexPositionColorTexture[] v, int[] idx, byte[] faces)
        {
            Clear();
            if (v == null || idx == null || idx.Length == 0 || faces == null) return;

            _verts = v;
            _face = faces;
            
            // Save a backup of the original vertex positions and attributes
            _baseVerts = new VertexPositionColorTexture[v.Length];
            Array.Copy(v, _baseVerts, v.Length);

            _base = new Color[v.Length];
            for (int i = 0; i < v.Length; i++) _base[i] = v[i].Color;

            _tris = idx.Length / 3;
            _vb = new DynamicVertexBuffer(device, typeof(VertexPositionColorTexture), v.Length, BufferUsage.WriteOnly);
            _ib = new IndexBuffer(device, IndexElementSize.ThirtyTwoBits, idx.Length, BufferUsage.WriteOnly);
            _ib.SetData(idx);
        }

        public void Update(Vector3 cam, float time)
        {
            if (_vb == null) return;

            float intensity = WaterWaveIntensity;
            bool animateWaves = intensity > 0.0001f;

            for (int i = 0; i < _verts.Length; i++)
            {
                // 1. Apply Perlin noise wave displacement ONLY to top water faces (face index 2 = +Y)
                if (animateWaves && _face[i] == 2)
                {
                    Vector3 basePos = _baseVerts[i].Position;
                    // Sample noise using world XZ coordinates and time for smooth movement
                    float noise = (float)WaterNoise.Noise(basePos.X * 0.35, basePos.Z * 0.35, time * WaterWaveSpeed);
                    _verts[i].Position.Y = basePos.Y + noise * intensity;
                }
                else
                {
                    _verts[i].Position = _baseVerts[i].Position;
                }

                // 2. Existing Fresnel-style alpha calculation
                Vector3 d = cam - _verts[i].Position;
                float dist = d.Length();
                float along = _face[i] < 2 ? d.X : _face[i] < 4 ? d.Y : d.Z; 
                float cos = dist > 0.001f ? MathF.Abs(along) / dist : 1f;

                float k = 1f - cos;
                k *= k; k *= k;                                
                float a = AlphaMin + (AlphaMax - AlphaMin) * k;

                Color c = _base[i];
                _verts[i].Color = new Color(c.R / 255f * a, c.G / 255f * a, c.B / 255f * a, a); 
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