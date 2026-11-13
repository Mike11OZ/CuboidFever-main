
using System;
using Microsoft.Xna.Framework;

namespace CuboidFever;

public class Player
{
    private const float Width = 0.6f, Height = 1.8f, EyeHeight = 1.62f;
    private const float WalkSpeed = 4.0f, FlySpeed = 24f;
    private const float Gravity = 28f, JumpSpeed = 8.5f, MaxFall = 50f;

    // Swimming
    private const float SwimSpeed = 2.5f;       // horizontal speed in water
    private const float SwimUpSpeed = 3.5f;     // holding jump
    private const float SwimDownSpeed = 3.5f;   // holding descend
    private const float SwimSinkSpeed = 1.5f;   // drifting down when no key is held
    private const float SwimAccel = 40f;        // how fast vertical speed eases toward its target
    private const float SurfaceHopSpeed = 6.5f; // jumping at the surface hops you out onto the shore
    private const float HeadCheckHeight = 1.4f; // swimming up stops once this point on the body leaves the water

    public Vector3 Position;   // center of the feet
    public Vector3 Velocity;
    public bool OnGround;
    public bool Flying;        // fly mode ignores collision
    public bool InWater;       // feet are in water (set each Update); other systems can use it for splashes, sounds, etc.

    public Vector3 Eye => Position + new Vector3(0, EyeHeight, 0);

    // 'wish' is the desired movement direction (not necessarily normalized);
    // 'vertical' is -1/0/+1 for fly mode down/none/up.
    public void Update(World world, float dt, Vector3 wish, bool jump, float vertical)
    {
        dt = MathF.Min(dt, 0.05f); // avoid huge steps after a hitch

        if (Flying)
        {
            if (wish != Vector3.Zero) wish.Normalize();
            Position += (wish * FlySpeed + new Vector3(0, vertical * FlySpeed, 0)) * dt;
            Velocity = Vector3.Zero;
            OnGround = false;
            return;
        }

        if (wish != Vector3.Zero) wish.Normalize();

        InWater = WaterSurface.IsUnderwater(world, Position + new Vector3(0, 0.1f, 0));

        if (InWater)
        {
            Velocity.X = wish.X * SwimSpeed;
            Velocity.Z = wish.Z * SwimSpeed;

            // Water holds you up: no gravity, just ease toward a target vertical speed.
            bool headInWater = WaterSurface.IsUnderwater(world, Position + new Vector3(0, HeadCheckHeight, 0));
            float target = vertical < 0 ? -SwimDownSpeed
                         : vertical > 0 && headInWater ? SwimUpSpeed
                         : -SwimSinkSpeed;

            Velocity.Y = MoveToward(Velocity.Y, target, SwimAccel * dt);

            // At the surface, jumping hops you up and out (so you can climb onto the bank).
            if (jump && !headInWater)
                Velocity.Y = MathF.Max(Velocity.Y, SurfaceHopSpeed);
        }
        else
        {
            Velocity.X = wish.X * WalkSpeed;
            Velocity.Z = wish.Z * WalkSpeed;

            Velocity.Y = MathF.Max(Velocity.Y - Gravity * dt, -MaxFall);
            if (jump && OnGround) Velocity.Y = JumpSpeed;
        }

        OnGround = false;
        Move(world, 0, Velocity.X * dt);
        Move(world, 1, Velocity.Y * dt);
        Move(world, 2, Velocity.Z * dt);
    }

    private static float MoveToward(float value, float target, float maxDelta) =>
        MathF.Abs(target - value) <= maxDelta ? target : value + MathF.Sign(target - value) * maxDelta;

    // True if the player's box overlaps the block at (x, y, z). Used to stop you placing blocks inside yourself.
    public bool Overlaps(int x, int y, int z)
    {
        float hw = Width / 2;
        return Position.X + hw > x && Position.X - hw < x + 1
            && Position.Y + Height > y && Position.Y < y + 1
            && Position.Z + hw > z && Position.Z - hw < z + 1;
    }

    // True if the player's box overlaps the box (min..max) given in world coordinates. Used to stop you placing
    // a door on top of yourself; unlike Overlaps(x, y, z) it can test a box smaller than a block.
    public bool OverlapsBox(Vector3 min, Vector3 max) => BlockCollision.Overlaps(Position, Width, Height, min, max);

    // Moves along one axis, stopping against solid blocks (the rules are shared with creatures: BlockCollision).
    private void Move(World world, int axis, float delta)
    {
        if (!BlockCollision.Move(world, ref Position, Width, Height, axis, delta, out bool landed)) return;

        if (landed) OnGround = true;

        if (axis == 0) Velocity.X = 0;
        else if (axis == 1) Velocity.Y = 0;
        else Velocity.Z = 0;
    }
}
