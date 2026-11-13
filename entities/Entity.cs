using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// What every entity of one kind shares: its model, its texture and how big it is in the world.
public sealed class EntityType
{
    public string Id { get; init; } = "";
    public EntityModel Model { get; init; } = null!;
    public Texture2D Texture { get; init; } = null!;

    // Blocks per model unit is Scale / 16, so 1 draws the model exactly as it looks in Blockbench
    // (a pixel is 1/16 of a block). Raise it to make the creature bigger.
    public float Scale { get; init; } = 1f;

    // Collision box in blocks at Scale 1 (it grows with Scale). Width is the same along X and Z.
    // Kept apart from the model on purpose: a tail or a claw sticking out should not stop a creature fitting
    // through a one-block gap.
    public float Width { get; init; } = 0.6f;
    public float Height { get; init; } = 0.6f;

    // Builds an entity of this type (position = feet, yaw in radians). This is what /spawn calls.
    public Func<EntityType, Vector3, float, Entity> Factory { get; init; } = null!;

    public Entity Create(Vector3 position, float yaw) => Factory(this, position, yaw);
}

// Anything that lives in the world and isn't a block: creatures now, maybe items and projectiles later.
// This base class only knows where the entity is, which way it faces and which animation it is playing.
public abstract class Entity
{
    public EntityType Type { get; }

    public Vector3 Position;   // centre of the feet, like Player.Position
    public float Yaw;          // radians, turning left (counter-clockwise from above). 0 faces -Z, like the player's yaw.

    public string Animation;   // name of the model animation being played, or null for the resting pose
    public float AnimTime;     // seconds since that animation started

    // Set to true to have the EntityManager drop this entity at the end of its update
    public bool Removed;

    // The manager this entity lives in (set when it is spawned). Gives access to the world and the player.
    public EntityManager Manager { get; internal set; }

    protected Entity(EntityType type, Vector3 position, float yaw = 0f)
    {
        Type = type;
        Position = position;
        Yaw = yaw;
    }

    // Model units -> blocks, turn to face Yaw, move to Position
    public Matrix WorldMatrix =>
        Matrix.CreateScale(Type.Scale / 16f) * Matrix.CreateRotationY(Yaw) * Matrix.CreateTranslation(Position);

    // A box that surely contains the whole drawn model (tail and claws included), used to skip entities that are off screen.
    public BoundingBox RenderBounds
    {
        get
        {
            float r = 1.2f * Type.Scale, h = 1.5f * Type.Scale;
            return new BoundingBox(Position - new Vector3(r, 0f, r), Position + new Vector3(r, h, r));
        }
    }

    // Switches animation, restarting it. Asking for the one already playing does nothing.
    public void Play(string animation)
    {
        if (Animation == animation) return;
        Animation = animation;
        AnimTime = 0f;
    }

    public virtual void Update(float dt, World world)
    {
        AnimTime += dt;
    }
}
