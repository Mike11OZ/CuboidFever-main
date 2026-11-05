using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

/// <summary>
/// Draws the sun and moon as part of the sky, not as objects in the world.
///
/// Why it's built this way:
///  - The sky is drawn FIRST, with no depth test and no depth write, and the
///    terrain is drawn over it afterwards. Hills, trees and water therefore
///    always cover the sun, and the sun can never poke through them no matter
///    how far you walk from the geometry.
///  - The view matrix has its translation removed, so the sky only responds to
///    where the camera is LOOKING, never to where it is standing.
///  - Each body is a quad fixed in the sky, facing the camera at the origin,
///    not a screen-aligned billboard. Its direction comes from
///    <see cref="DayNightCycle.SunDirection"/>, so there is one source of truth
///    for where the sun is.
///  - Bodies are sized by angular diameter (how big they look), so changing
///    <see cref="SkyDistance"/> never changes their apparent size.
/// </summary>
public sealed class SkyRenderer
{
    private const float SunAngularSizeDegrees = 12f;
    private const float MoonAngularSizeDegrees = 12f;

    private const float OrbitTiltDegrees = 0f;
    private const float SkyDistance = 100f;

    private const float HorizonFadeStart = -0.35f;
    private const float HorizonFadeEnd = -0.15f;

    private static readonly short[] QuadIndices = { 0, 1, 2, 0, 2, 3 };

    private readonly GraphicsDevice _gd;
    private readonly BasicEffect _effect;
    private readonly VertexPositionColorTexture[] _vertices = new VertexPositionColorTexture[4];

    private Texture2D _sunTexture;
    private Texture2D _moonTexture;

    public SkyRenderer(GraphicsDevice graphicsDevice)
    {
        _gd = graphicsDevice;

        _effect = new BasicEffect(_gd)
        {
            VertexColorEnabled = true,
            TextureEnabled = true,
            World = Matrix.Identity
        };

        string dir = Path.Combine(AppContext.BaseDirectory, "assets", "cuboidfever", "textures", "ambient");
        _sunTexture = LoadTexture(Path.Combine(dir, "sol.png"));
        _moonTexture = LoadTexture(Path.Combine(dir, "luna.png"));
    }

    private Texture2D LoadTexture(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"[Sky] Missing texture: {path}");
                return null;
            }

            using var stream = File.OpenRead(path);
            return Texture2D.FromStream(_gd, stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Sky] Could not load {path}: {ex.Message}");
            return null;
        }
    }

    public void Draw(Matrix view, Matrix projection, DayNightCycle dayNight)
    {
        if (_sunTexture == null && _moonTexture == null)
            return;

        view.Translation = Vector3.Zero;

        _effect.View = view;
        _effect.Projection = projection;

        _gd.BlendState = BlendState.AlphaBlend;
        _gd.DepthStencilState = DepthStencilState.None;
        _gd.RasterizerState = RasterizerState.CullNone;
        _gd.SamplerStates[0] = SamplerState.PointClamp;

        Matrix tilt = Matrix.CreateRotationY(MathHelper.ToRadians(OrbitTiltDegrees));
        Vector3 sunDir = Vector3.Normalize(Vector3.Transform(dayNight.SunDirection, tilt));
        Vector3 orbitAxis = Vector3.Transform(Vector3.UnitX, tilt);

        DrawBody(sunDir, orbitAxis, _sunTexture, SunAngularSizeDegrees);
        DrawBody(-sunDir, orbitAxis, _moonTexture, MoonAngularSizeDegrees);
    }

    private void DrawBody(Vector3 direction, Vector3 orbitAxis, Texture2D texture, float angularSizeDegrees)
    {
        if (texture == null)
            return;

        float fade = MathHelper.SmoothStep(0f, 1f,
            Math.Clamp((direction.Y - HorizonFadeStart) / (HorizonFadeEnd - HorizonFadeStart), 0f, 1f));

        if (fade <= 0f)
            return;

        float half = SkyDistance * MathF.Tan(MathHelper.ToRadians(angularSizeDegrees) * 0.5f);
        Vector3 center = direction * SkyDistance;
        Vector3 right = orbitAxis * half;
        Vector3 up = Vector3.Cross(orbitAxis, direction) * half;

        Color tint = Color.White * fade;

        _vertices[0] = new VertexPositionColorTexture(center - right + up, tint, new Vector2(0, 0));
        _vertices[1] = new VertexPositionColorTexture(center + right + up, tint, new Vector2(1, 0));
        _vertices[2] = new VertexPositionColorTexture(center + right - up, tint, new Vector2(1, 1));
        _vertices[3] = new VertexPositionColorTexture(center - right - up, tint, new Vector2(0, 1));

        _effect.Texture = texture;

        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, 0, 4, QuadIndices, 0, 2);
        }
    }
}