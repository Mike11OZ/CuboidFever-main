using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// Owns every entity in the current world: updates them each frame and draws them.
// One manager per world session (Game1.CreateWorldSession), so entities never leak into the next world.
public sealed class EntityManager : IDisposable
{
    private readonly List<Entity> _entities = new();
    private readonly AlphaTestEffect _effect;

    public IReadOnlyList<Entity> All => _entities;
    public int Count => _entities.Count;

    // What creatures can look at and react to
    public World World { get; }
    public Player Player { get; }

    public EntityManager(GraphicsDevice device, World world, Player player)
    {
        World = world;
        Player = player;

        // The same see-through cutoff the terrain uses (alpha under 128 is not drawn)
        _effect = new AlphaTestEffect(device)
        {
            VertexColorEnabled = true,
            AlphaFunction = CompareFunction.GreaterEqual,
            ReferenceAlpha = 128
        };
    }

    public T Spawn<T>(T entity) where T : Entity
    {
        entity.Manager = this;
        _entities.Add(entity);
        return entity;
    }

    public void Update(float dt, World world)
    {
        // Index loop so an entity may spawn another during its update without breaking the iteration
        for (int i = 0; i < _entities.Count; i++)
            _entities[i].Update(dt, world);

        _entities.RemoveAll(e => e.Removed);
    }

    // Draws after the terrain and before water, so the water's depth pass can still hide what is underneath it.
    public void Draw(GraphicsDevice device, Matrix view, Matrix projection, BoundingFrustum frustum,
                     World world, DayNightCycle dayNight)
    {
        if (_entities.Count == 0) return;

        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.Default;
        device.RasterizerState = RasterizerState.CullCounterClockwise;
        device.SamplerStates[0] = SamplerState.PointClamp;

        _effect.View = view;
        _effect.Projection = projection;

        foreach (var e in _entities)
        {
            if (!frustum.Intersects(e.RenderBounds)) continue;

            var model = e.Type.Model;
            var vertices = model.BuildPose(e.Animation, e.AnimTime);

            _effect.Texture = e.Type.Texture;
            _effect.World = e.WorldMatrix;
            _effect.DiffuseColor = LightAt(world, dayNight, e.Position);
            _effect.Alpha = 1f;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, model.VertexCount,
                                                 model.Indices, 0, model.TriangleCount);
            }
        }
    }

    // How bright the spot an entity stands in is, using the terrain's own rule (see SolidShader.fx):
    // the stronger of the lamp light (per colour) and the sky light, which dims at night.
    private static Vector3 LightAt(World world, DayNightCycle dayNight, Vector3 position)
    {
        int x = (int)MathF.Floor(position.X);
        int y = (int)MathF.Floor(position.Y + 0.3f); // a little above the feet, so we read the air and not the floor
        int z = (int)MathF.Floor(position.Z);

        float sky = Chunk.Curve[world.Light.Get(0, x, y, z)] * dayNight.SkyBrightness;
        return new Vector3(
            MathF.Max(Chunk.Curve[world.Light.Get(1, x, y, z)], sky),
            MathF.Max(Chunk.Curve[world.Light.Get(2, x, y, z)], sky),
            MathF.Max(Chunk.Curve[world.Light.Get(3, x, y, z)], sky));
    }

    public void Dispose() => _effect.Dispose();
}
