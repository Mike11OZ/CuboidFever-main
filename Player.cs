
using System;
using Microsoft.Xna.Framework;

namespace CuboidFever;

public class Player
{
    private const float Width = 0.6f, Height = 1.8f, EyeHeight = 1.62f;
    private const float WalkSpeed = 4.0f, FlySpeed = 24f;
    private const float Gravity = 28f, JumpSpeed = 8.5f, MaxFall = 50f;
    private const float Eps = 0.001f;

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
    public bool OverlapsBox(Vector3 min, Vector3 max)
    {
        float hw = Width / 2;
        return Position.X + hw > min.X && Position.X - hw < max.X
            && Position.Y + Height > min.Y && Position.Y < max.Y
            && Position.Z + hw > min.Z && Position.Z - hw < max.Z;
    }

    private bool Collides(World world)
    {
        float hw = Width / 2;
        int x0 = (int)MathF.Floor(Position.X - hw), x1 = (int)MathF.Floor(Position.X + hw);
        int y0 = (int)MathF.Floor(Position.Y),      y1 = (int)MathF.Floor(Position.Y + Height);
        int z0 = (int)MathF.Floor(Position.Z - hw), z1 = (int)MathF.Floor(Position.Z + hw);
        var info = BlockRegistry.Instance.Info;

        for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
        for (int z = z0; z <= z1; z++)
        {
            byte id = world.GetBlock(x, y, z);
            if (!info[id].Solid) continue;

            // A door only fills part of its block, so only count it when the player touches the slab itself
            if (info[id].IsDoor)
            {
                BlockShapes.DoorBox(in info[id], x, y, z, out var min, out var max);
                if (!OverlapsBox(min, max)) continue;
            }

            return true;
        }

        return false;
    }

    // After a move along 'axis' ran into something, finds where the player should stop: just short of the nearest
    // face of whatever they overlap (moving up the axis), or just past the farthest one (moving down it). A normal
    // block's faces are on whole numbers; a door's are not.
    private float ContactLimit(World world, int axis, bool positive)
    {
        float hw = Width / 2;
        int x0 = (int)MathF.Floor(Position.X - hw), x1 = (int)MathF.Floor(Position.X + hw);
        int y0 = (int)MathF.Floor(Position.Y),      y1 = (int)MathF.Floor(Position.Y + Height);
        int z0 = (int)MathF.Floor(Position.Z - hw), z1 = (int)MathF.Floor(Position.Z + hw);
        var info = BlockRegistry.Instance.Info;

        float limit = positive ? float.PositiveInfinity : float.NegativeInfinity;

        for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
        for (int z = z0; z <= z1; z++)
        {
            byte id = world.GetBlock(x, y, z);
            if (!info[id].Solid) continue;

            Vector3 min = new(x, y, z), max = new(x + 1, y + 1, z + 1);

            if (info[id].IsDoor)
            {
                BlockShapes.DoorBox(in info[id], x, y, z, out min, out max);
                if (!OverlapsBox(min, max)) continue;
            }

            float face = positive
                ? (axis == 0 ? min.X : axis == 1 ? min.Y : min.Z)
                : (axis == 0 ? max.X : axis == 1 ? max.Y : max.Z);

            limit = positive ? MathF.Min(limit, face) : MathF.Max(limit, face);
        }

        return limit;
    }

    private float Get(int a) => a == 0 ? Position.X : a == 1 ? Position.Y : Position.Z;

    private void Set(int a, float v)
    {
        if (a == 0) Position.X = v;
        else if (a == 1) Position.Y = v;
        else Position.Z = v;
    }

    // Moves along one axis in small sub-steps (so a fast fall can't tunnel through a floor),
    // snapping to the surface of whatever block it hits.
    private void Move(World world, int axis, float delta)
    {
        if (delta == 0) return;

        int steps = (int)MathF.Ceiling(MathF.Abs(delta) / 0.4f);
        float d = delta / steps;
        float hw = Width / 2;
        float upper = axis == 1 ? Height : hw;
        float lower = axis == 1 ? 0 : hw;

        for (int i = 0; i < steps; i++)
        {
            Set(axis, Get(axis) + d);
            if (!Collides(world)) continue;

            if (d > 0)
            {
                Set(axis, ContactLimit(world, axis, positive: true) - upper - Eps);
            }
            else
            {
                Set(axis, ContactLimit(world, axis, positive: false) + lower + Eps);
                if (axis == 1) OnGround = true;
            }

            if (axis == 0) Velocity.X = 0;
            else if (axis == 1) Velocity.Y = 0;
            else Velocity.Z = 0;

            return;
        }
    }
}
