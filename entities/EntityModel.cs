using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// A BbModel turned into triangles, plus the maths to pose it.
//
// The boxes are built once. Each frame BuildPose works out where every bone is (its resting pose plus the
// current animation) and moves the vertices to match. A model has a few hundred vertices, so doing this on
// the CPU is cheap and needs no skinning shader.
//
// The vertices come out in model units (1/16 block). The caller scales them to blocks and places the
// entity with a world matrix (Entity.WorldMatrix).
public sealed class EntityModel
{
    public BbModel Source { get; }
    public int VertexCount => _rest.Length;
    public int TriangleCount => _indices.Length / 3;
    public short[] Indices => _indices;

    // Resting vertex positions (each box's own rotation already applied), and what each vertex needs
    private readonly Vector3[] _rest;
    private readonly Vector2[] _uv;
    private readonly Color[] _shade;
    private readonly int[] _bone;
    private readonly short[] _indices;

    private readonly Matrix[] _boneMatrices;
    private readonly VertexPositionColorTexture[] _posed;

    // Outward normal of each face, indexed by BbFaceDir
    private static readonly Vector3[] FaceNormals =
    {
        new(0, 0, -1), new(1, 0, 0), new(0, 0, 1), new(-1, 0, 0), new(0, 1, 0), new(0, -1, 0)
    };

    public EntityModel(BbModel source)
    {
        Source = source;

        var rest = new List<Vector3>();
        var uv = new List<Vector2>();
        var shade = new List<Color>();
        var bone = new List<int>();
        var indices = new List<short>();

        for (int b = 0; b < source.Bones.Count; b++)
            foreach (var cube in source.Bones[b].Cubes)
                AddCube(source, cube, b, rest, uv, shade, bone, indices);

        if (rest.Count > short.MaxValue)
            throw new InvalidOperationException($"Model '{source.Name}' has too many vertices ({rest.Count}).");

        _rest = rest.ToArray();
        _uv = uv.ToArray();
        _shade = shade.ToArray();
        _bone = bone.ToArray();
        _indices = indices.ToArray();

        _boneMatrices = new Matrix[source.Bones.Count];
        _posed = new VertexPositionColorTexture[_rest.Length];
    }

    // =====================  BUILDING  =====================

    private static void AddCube(BbModel model, BbCube cube, int boneIndex, List<Vector3> rest, List<Vector2> uv,
                                List<Color> shade, List<int> bone, List<short> indices)
    {
        // The cube's own rotation: turn it around its origin
        Matrix own = Matrix.CreateTranslation(-cube.Origin) * RotationMatrix(cube.RotationDeg) *
                     Matrix.CreateTranslation(cube.Origin);

        Vector3 min = cube.From, max = cube.To;
        Vector3 P(int x, int y, int z) => new(x == 0 ? min.X : max.X, y == 0 ? min.Y : max.Y, z == 0 ? min.Z : max.Z);

        // The four corners of each face as seen from outside: top-left, top-right, bottom-left, bottom-right.
        // These match Blockbench's own corner order, which is what the face UVs are written against.
        Vector3[][] corners =
        {
            /* north */ new[] { P(1, 1, 0), P(0, 1, 0), P(1, 0, 0), P(0, 0, 0) },
            /* east  */ new[] { P(1, 1, 1), P(1, 1, 0), P(1, 0, 1), P(1, 0, 0) },
            /* south */ new[] { P(0, 1, 1), P(1, 1, 1), P(0, 0, 1), P(1, 0, 1) },
            /* west  */ new[] { P(0, 1, 0), P(0, 1, 1), P(0, 0, 0), P(0, 0, 1) },
            /* up    */ new[] { P(0, 1, 0), P(1, 1, 0), P(0, 1, 1), P(1, 1, 1) },
            /* down  */ new[] { P(0, 0, 0), P(1, 0, 0), P(0, 0, 1), P(1, 0, 1) },
        };

        float w = model.TextureWidth, h = model.TextureHeight;

        for (int f = 0; f < 6; f++)
        {
            BbFace face = cube.Faces[f];
            if (!face.Visible) continue;

            Vector3 tl = corners[f][0], tr = corners[f][1], bl = corners[f][2], br = corners[f][3];
            Vector3 normal = FaceNormals[f];

            // Same light levels the terrain uses for its faces: top brightest, bottom darkest
            float s = normal.Y > 0.5f ? 1.0f : normal.Y < -0.5f ? 0.5f : MathF.Abs(normal.X) > 0.5f ? 0.8f : 0.65f;
            var color = new Color(s, s, s);

            int first = rest.Count;

            // Order: top-left, top-right, bottom-right, bottom-left
            Vector3[] quad = { tl, tr, br, bl };
            Vector2[] quadUv =
            {
                new(face.U1 / w, face.V1 / h), new(face.U2 / w, face.V1 / h),
                new(face.U2 / w, face.V2 / h), new(face.U1 / w, face.V2 / h)
            };

            for (int i = 0; i < 4; i++)
            {
                rest.Add(Vector3.Transform(quad[i], own));
                uv.Add(quadUv[i]);
                shade.Add(color);
                bone.Add(boneIndex);
            }

            // The game culls counter-clockwise triangles (the usual XNA setting), so a visible face must be wound
            // clockwise when seen from outside. Seen from outside, top-left -> bottom-left -> bottom-right goes
            // counter-clockwise and its right-hand normal points at the viewer. So if that normal agrees with the
            // outward normal, the order is flipped. Working it out per face means a mirrored UV can't break it.
            Vector3 right = Vector3.Cross(bl - tl, br - tl);
            bool flip = Vector3.Dot(right, normal) > 0f;

            if (!flip)
            {
                indices.Add((short)first); indices.Add((short)(first + 3)); indices.Add((short)(first + 2));
                indices.Add((short)first); indices.Add((short)(first + 2)); indices.Add((short)(first + 1));
            }
            else
            {
                indices.Add((short)first); indices.Add((short)(first + 2)); indices.Add((short)(first + 3));
                indices.Add((short)first); indices.Add((short)(first + 1)); indices.Add((short)(first + 2));
            }
        }
    }

    // Blockbench turns X first, then Y, then Z.
    private static Matrix RotationMatrix(Vector3 degrees) =>
        Matrix.CreateRotationX(MathHelper.ToRadians(degrees.X)) *
        Matrix.CreateRotationY(MathHelper.ToRadians(degrees.Y)) *
        Matrix.CreateRotationZ(MathHelper.ToRadians(degrees.Z));

    // =====================  POSING  =====================

    // Works out the model's vertices for an animation at a moment in time. A null or unknown animation name
    // gives the resting pose. The returned array is reused by the next call, so draw it before posing again.
    public VertexPositionColorTexture[] BuildPose(string animationName, float time)
    {
        BbAnimation anim = null;
        if (animationName != null) Source.Animations.TryGetValue(animationName, out anim);
        float t = anim?.Wrap(time) ?? 0f;

        var bones = Source.Bones;
        for (int i = 0; i < bones.Count; i++)
        {
            Vector3 rotation = bones[i].RotationDeg;
            Vector3 offset = Vector3.Zero;

            // Animation values are added on top of the bone's resting pose
            if (anim != null && anim.Tracks.TryGetValue(i, out var track))
            {
                rotation += BbTrack.Sample(track.Rotation, t);
                offset += BbTrack.Sample(track.Position, t);
            }

            Vector3 pivot = bones[i].Origin;

            // Turn around the pivot, then (row-vector maths) carry on with the parent's movement
            Matrix local = Matrix.CreateTranslation(-pivot) * RotationMatrix(rotation) *
                           Matrix.CreateTranslation(pivot + offset);

            int parent = bones[i].Parent;
            _boneMatrices[i] = parent < 0 ? local : local * _boneMatrices[parent];
        }

        for (int v = 0; v < _rest.Length; v++)
            _posed[v] = new VertexPositionColorTexture(Vector3.Transform(_rest[v], _boneMatrices[_bone[v]]), _shade[v], _uv[v]);

        return _posed;
    }
}
