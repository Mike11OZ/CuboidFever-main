using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// Draws the 3D world: terrain, the block outline, particles, water, and celestial bodies.
// It owns the camera projection and the effects used to draw them.
public sealed class WorldRenderer
{
    private const float FovDegrees = 80f;
    private const float NearPlane = 0.1f;
    private const float FarPlane = 500f;

    private readonly GraphicsDevice _gd;
    private readonly Effect _solidEffect;
    private readonly Effect _waterEffect;
    private readonly BasicEffect _effect;     // holds View/Projection; also used to draw particles and sky
    private readonly BasicEffect _lineEffect; // block outline
    private readonly Texture2D _atlas;
    private readonly World _world;
    private readonly PlayerController _controller;
    private readonly BlockInteraction _interaction;
    private readonly ParticleSystem _particles;
    private readonly DayNightCycle _dayNight;

    private readonly SkyRenderer _sky;

    public WorldRenderer(GraphicsDevice graphicsDevice, Effect solidEffect, Effect waterEffect,
                         Texture2D atlas, World world, PlayerController controller,
                         BlockInteraction interaction, ParticleSystem particles, DayNightCycle dayNight)
    {
        _gd = graphicsDevice;
        _solidEffect = solidEffect;
        _waterEffect = waterEffect;
        _atlas = atlas;
        _world = world;
        _controller = controller;
        _interaction = interaction;
        _particles = particles;
        _dayNight = dayNight;

        _effect = new BasicEffect(_gd)
        {
            VertexColorEnabled = true,
            TextureEnabled = true,
            Texture = _atlas,
            Projection = CreateProjection()
        };

        _lineEffect = new BasicEffect(_gd) { VertexColorEnabled = true };

        _sky = new SkyRenderer(_gd);
    }

    private Matrix CreateProjection() =>
        Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(FovDegrees), _gd.Viewport.AspectRatio, NearPlane, FarPlane);

    // Call when the window size changes so the aspect ratio stays correct.
    public void OnResize()
    {
        if (_gd.Viewport.Width > 0 && _gd.Viewport.Height > 0)
            _effect.Projection = CreateProjection();
    }

    public void Draw(float totalTime)
    {
        _effect.View = _controller.View;
        Matrix viewProjection = _effect.View * _effect.Projection;

        var frustum = new BoundingFrustum(viewProjection);
        _world.UpdateVisibility(frustum, _controller.CamPos, totalTime);

        _gd.Clear(_dayNight.SkyColor);
        _gd.SamplerStates[0] = SamplerState.PointClamp;

        // Sky first, with no depth, so the world always draws over the sun and moon.
        _sky.Draw(_effect.View, _effect.Projection, _dayNight);

        DrawSolid(viewProjection);
        if (_interaction.HasTarget) DrawTargetOutline(_interaction.Target);
        _particles.Draw(_gd, _effect, frustum, _controller.Right, _controller.Up);
        DrawWater(viewProjection, totalTime);
    }

    private void DrawSolid(Matrix viewProjection)
    {
        _gd.BlendState = BlendState.Opaque;
        _gd.DepthStencilState = DepthStencilState.Default;
        _gd.RasterizerState = RasterizerState.CullCounterClockwise;

        _solidEffect.Parameters["WorldViewProjection"]?.SetValue(viewProjection);
        _solidEffect.Parameters["SpriteTexture"]?.SetValue(_atlas);
        _solidEffect.Parameters["SkyBrightness"]?.SetValue(_dayNight.SkyBrightness);

        foreach (var pass in _solidEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _world.DrawSolid(_gd);
        }
    }

    private void DrawWater(Matrix viewProjection, float totalTime)
    {
        _gd.RasterizerState = RasterizerState.CullNone;

        WaterWaves.Apply(_waterEffect, viewProjection, _controller.CamPos, _atlas, totalTime);
        _waterEffect.Parameters["SkyBrightness"]?.SetValue(_dayNight.SkyBrightness);

        // Pass 1: depth only
        _gd.BlendState = DepthOnlyBlend;
        _gd.DepthStencilState = DepthStencilState.Default;
        foreach (var pass in _waterEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _world.DrawWater(_gd);
        }

        // Pass 2: colour
        _gd.BlendState = BlendState.AlphaBlend;
        _gd.DepthStencilState = DepthStencilState.DepthRead;
        foreach (var pass in _waterEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _world.DrawWater(_gd);
        }
    }

    private static readonly BlendState DepthOnlyBlend = new() { ColorWriteChannels = ColorWriteChannels.None };

    // Black wireframe box around the block being looked at.
    private void DrawTargetOutline((int x, int y, int z) target)
    {
        var min = new Vector3(target.x, target.y, target.z) - new Vector3(0.002f);
        var max = min + new Vector3(1.004f);

        var p = new Vector3[8];
        for (int i = 0; i < 8; i++)
            p[i] = new Vector3((i & 1) == 0 ? min.X : max.X,
                               (i & 2) == 0 ? min.Y : max.Y,
                               (i & 4) == 0 ? min.Z : max.Z);

        int[] edges = { 0,1, 2,3, 4,5, 6,7,   0,2, 1,3, 4,6, 5,7,   0,4, 1,5, 2,6, 3,7 };
        var v = new VertexPositionColor[24];
        for (int i = 0; i < 24; i++) v[i] = new VertexPositionColor(p[edges[i]], Color.Black);

        _lineEffect.View = _effect.View;
        _lineEffect.Projection = _effect.Projection;
        foreach (var pass in _lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
            _gd.DrawUserPrimitives(PrimitiveType.LineList, v, 0, 12);
        }
    }
}