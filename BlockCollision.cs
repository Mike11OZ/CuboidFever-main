using System;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// Moving a box through the block world without going into solid blocks. Shared by the player and by creatures
// (MobEntity), so both collide with exactly the same rules: solid blocks are full cubes, doors fill only their slab.
//
// A body is described by where its feet are (the centre of the bottom face), its width (the same in X and Z)
// and its height. Nothing here keeps state; callers own the position and velocity.
public static class BlockCollision
{
    private const float Eps = 0.001f;

    // True if the body overlaps the box min..max (world coordinates)
    public static bool Overlaps(Vector3 feet, float width, float height, Vector3 min, Vector3 max)
    {
        float hw = width / 2;
        return feet.X + hw > min.X && feet.X - hw < max.X
            && feet.Y + height > min.Y && feet.Y < max.Y
            && feet.Z + hw > min.Z && feet.Z - hw < max.Z;
    }

    // True if the body is inside any solid block
    public static bool Collides(World world, Vector3 feet, float width, float height)
    {
        float hw = width / 2;
        int x0 = (int)MathF.Floor(feet.X - hw), x1 = (int)MathF.Floor(feet.X + hw);
        int y0 = (int)MathF.Floor(feet.Y),      y1 = (int)MathF.Floor(feet.Y + height);
        int z0 = (int)MathF.Floor(feet.Z - hw), z1 = (int)MathF.Floor(feet.Z + hw);
        var info = BlockRegistry.Instance.Info;

        for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
        for (int z = z0; z <= z1; z++)
        {
            byte id = world.GetBlock(x, y, z);
            if (!info[id].Solid) continue;

            // A door only fills part of its block, so only count it when the body touches the slab itself
            if (info[id].IsDoor)
            {
                BlockShapes.DoorBox(in info[id], x, y, z, out var min, out var max);
                if (!Overlaps(feet, width, height, min, max)) continue;
            }

            return true;
        }

        return false;
    }

    // After a move along 'axis' (0 = X, 1 = Y, 2 = Z) ran into something, finds where the body should stop: just
    // short of the nearest face of whatever it overlaps (moving up the axis), or just past the farthest one
    // (moving down it). A normal block's faces are on whole numbers; a door's are not.
    private static float ContactLimit(World world, Vector3 feet, float width, float height, int axis, bool positive)
    {
        float hw = width / 2;
        int x0 = (int)MathF.Floor(feet.X - hw), x1 = (int)MathF.Floor(feet.X + hw);
        int y0 = (int)MathF.Floor(feet.Y),      y1 = (int)MathF.Floor(feet.Y + height);
        int z0 = (int)MathF.Floor(feet.Z - hw), z1 = (int)MathF.Floor(feet.Z + hw);
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
                if (!Overlaps(feet, width, height, min, max)) continue;
            }

            float face = positive
                ? (axis == 0 ? min.X : axis == 1 ? min.Y : min.Z)
                : (axis == 0 ? max.X : axis == 1 ? max.Y : max.Z);

            limit = positive ? MathF.Min(limit, face) : MathF.Max(limit, face);
        }

        return limit;
    }

    // Moves the body along one axis by 'delta', in small sub-steps (so a fast fall can't tunnel through a floor),
    // stopping against the first thing it hits. Returns true if it was stopped. 'landed' is true when it was
    // stopped while moving down, i.e. the body is now standing on something.
    public static bool Move(World world, ref Vector3 feet, float width, float height, int axis, float delta, out bool landed)
    {
        landed = false;
        if (delta == 0) return false;

        int steps = (int)MathF.Ceiling(MathF.Abs(delta) / 0.4f);
        float d = delta / steps;
        float hw = width / 2;
        float upper = axis == 1 ? height : hw;
        float lower = axis == 1 ? 0 : hw;

        for (int i = 0; i < steps; i++)
        {
            Set(ref feet, axis, Get(feet, axis) + d);
            if (!Collides(world, feet, width, height)) continue;

            if (d > 0)
            {
                Set(ref feet, axis, ContactLimit(world, feet, width, height, axis, positive: true) - upper - Eps);
            }
            else
            {
                Set(ref feet, axis, ContactLimit(world, feet, width, height, axis, positive: false) + lower + Eps);
                landed = axis == 1;
            }

            return true;
        }

        return false;
    }

    private static float Get(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;

    private static void Set(ref Vector3 v, int axis, float value)
    {
        if (axis == 0) v.X = value;
        else if (axis == 1) v.Y = value;
        else v.Z = value;
    }
}
