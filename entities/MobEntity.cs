using System;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// An entity that is pushed around by the world: it falls, stands on blocks, is stopped by walls, hops up one-block
// ledges and swims. It uses the same collision rules as the player (BlockCollision).
//
// A MobEntity does not decide where to go. Whatever drives it (a behaviour, later the AI goals) sets Wish each
// frame: the direction it would like to walk, flat along the ground. This class turns that into movement.
public abstract class MobEntity : Entity
{
    // Same feel as the player (Player.cs), so a one-block ledge that stops you also stops them
    private const float Gravity = 28f, MaxFall = 50f;
    private const float JumpSpeed = 8.5f;          // clears a 1-block ledge
    private const float SwimUpSpeed = 2.5f;        // paddling toward the surface
    private const float SwimSinkSpeed = 0.5f;      // drifting down once the head is out, so it bobs at the surface
    private const float SwimAccel = 40f;           // how fast vertical speed eases toward its target
    private const float SurfaceHopSpeed = 6.5f;    // hopping out of the water onto a bank
    private const float SwimSpeedFactor = 0.6f;    // walking speed is multiplied by this in water
    private const float TurnSpeed = 6f;            // radians per second the body can turn toward its heading
    private const float VoidY = -20f;              // below this the entity is removed

    public Vector3 Velocity;
    public bool OnGround;      // standing on something (set each update)
    public bool InWater;       // feet are in water (set each update)

    // Where this entity wants to walk. Only X and Z are used, and its length is capped at 1.
    public Vector3 Wish;

    public float MoveSpeed = 1.5f; // blocks per second at full Wish

    // What it is doing and how it gets there. Subclasses add goals in their constructor, highest priority first.
    public GoalSelector Goals { get; } = new();
    public Navigator Nav { get; }

    // A point to face while standing still (set by a goal each frame; cleared before the goals run). While walking
    // the body faces the way it is going instead.
    public Vector3? LookTarget;

    // Collision box, in blocks. Taken from the type, scaled with the model.
    public float Width { get; }
    public float Height { get; }

    // How fast it is moving along the ground, in blocks per second
    public float HorizontalSpeed => MathF.Sqrt(Velocity.X * Velocity.X + Velocity.Z * Velocity.Z);

    protected MobEntity(EntityType type, Vector3 position, float yaw = 0f) : base(type, position, yaw)
    {
        Width = type.Width * type.Scale;
        Height = type.Height * type.Scale;
        Nav = new Navigator(this);
    }

    public override void Update(float dt, World world)
    {
        // Think, then steer, then move
        LookTarget = null;
        Goals.Tick(this, dt);
        Nav.Tick(dt);

        base.Update(dt, world);
        Simulate(MathF.Min(dt, 0.05f), world); // a long hitch must not make one huge step
    }

    private void Simulate(float dt, World world)
    {
        // Nothing to stand on until the ground has loaded, so wait instead of falling through the world
        if (!world.IsColumnLoaded((int)MathF.Floor(Position.X), (int)MathF.Floor(Position.Z))) return;

        Vector3 wish = new(Wish.X, 0f, Wish.Z);
        if (wish.LengthSquared() > 1f) wish.Normalize();

        TurnToward(wish, dt);

        InWater = WaterSurface.IsUnderwater(world, Position + new Vector3(0, 0.1f, 0));
        bool headInWater = false;

        if (InWater)
        {
            float speed = MoveSpeed * SwimSpeedFactor;
            Velocity.X = wish.X * speed;
            Velocity.Z = wish.Z * speed;

            // Water holds it up: no gravity, just ease toward a target vertical speed
            headInWater = WaterSurface.IsUnderwater(world, Position + new Vector3(0, Height * 0.8f, 0));
            float target = headInWater ? SwimUpSpeed : -SwimSinkSpeed;
            Velocity.Y = MoveToward(Velocity.Y, target, SwimAccel * dt);
        }
        else
        {
            Velocity.X = wish.X * MoveSpeed;
            Velocity.Z = wish.Z * MoveSpeed;
            Velocity.Y = MathF.Max(Velocity.Y - Gravity * dt, -MaxFall);
        }

        bool wasOnGround = OnGround;
        OnGround = false;

        // Same order as the player: X, then Y, then Z
        bool blockedX = Move(world, 0, Velocity.X * dt);
        Move(world, 1, Velocity.Y * dt);
        bool blockedZ = Move(world, 2, Velocity.Z * dt);

        // Walked into a wall while trying to go somewhere: hop, in case it is only one block high
        if ((blockedX || blockedZ) && wish != Vector3.Zero)
        {
            if (!InWater && wasOnGround) Velocity.Y = JumpSpeed;
            else if (InWater && !headInWater) Velocity.Y = MathF.Max(Velocity.Y, SurfaceHopSpeed);
        }

        if (Position.Y < VoidY) Removed = true;
    }

    // Moves along one axis and stops against blocks. Returns whether it was stopped.
    private bool Move(World world, int axis, float delta)
    {
        if (!BlockCollision.Move(world, ref Position, Width, Height, axis, delta, out bool landed)) return false;

        if (landed) OnGround = true;

        if (axis == 0) Velocity.X = 0;
        else if (axis == 1) Velocity.Y = 0;
        else Velocity.Z = 0;

        return true;
    }

    // Swings the body toward the way it wants to walk. The model's front is -Z at yaw 0, and a yaw of y turns
    // -Z into (-sin y, -cos y), so the yaw that faces a direction (x, z) is atan2(-x, -z).
    private void TurnToward(Vector3 wish, float dt)
    {
        float x = wish.X, z = wish.Z;

        // Standing still: face whatever it was told to look at
        if (x == 0f && z == 0f && LookTarget.HasValue)
        {
            x = LookTarget.Value.X - Position.X;
            z = LookTarget.Value.Z - Position.Z;
        }

        if (x == 0f && z == 0f) return;

        float target = MathF.Atan2(-x, -z);
        float diff = MathHelper.WrapAngle(target - Yaw);
        float step = TurnSpeed * dt;

        Yaw = MathHelper.WrapAngle(Yaw + MathHelper.Clamp(diff, -step, step));
    }

    private static float MoveToward(float value, float target, float maxDelta) =>
        MathF.Abs(target - value) <= maxDelta ? target : value + MathF.Sign(target - value) * maxDelta;
}
