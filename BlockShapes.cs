using System;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// Geometry for blocks that are not a full cube. Today that is only the door: a 3/16 thick slab that fills the
// block's height and one of its edges. Closed it lies along the edge behind the player who placed it; open it has
// swung round to the edge on its hinge side (left or right of the player who placed it), so it is out of the doorway.
// Two doors side by side with their hinges on opposite sides form a double door.
//
// Facing is stored as 0..3 in BlockInfo, clockwise seen from above: north (-Z), east (+X), south (+Z), west (-X).
// A door's "facing" is the way the player was looking when they placed it.
public static class BlockShapes
{
    public const float DoorThickness = 3f / 16f;

    public static readonly string[] FacingNames = { "north", "east", "south", "west" };
    private static readonly int[] FacingDX = { 0, 1, 0, -1 };
    private static readonly int[] FacingDZ = { -1, 0, 1, 0 };

    // The block-offset (dx, dz) of the left-hand side of a door with this facing, as seen by the player who placed it.
    public static (int dx, int dz) LeftOf(int facing) => (FacingDZ[facing], -FacingDX[facing]);

    public static int FacingFromName(string name) => Array.IndexOf(FacingNames, name);

    // The facing closest to a horizontal look direction.
    public static int FacingFromDirection(float dx, float dz)
    {
        if (MathF.Abs(dx) > MathF.Abs(dz)) return dx > 0 ? 1 : 3;
        return dz > 0 ? 2 : 0;
    }

    // Box of the door block in block-local coordinates (0..1 on every axis).
    public static void DoorBox(int facing, bool open, bool hingeRight, out Vector3 min, out Vector3 max)
    {
        int fx = FacingDX[facing], fz = FacingDZ[facing];

        // Which edge the slab hugs: behind the player when closed, on the hinge side when open.
        int ex, ez;
        if (open) { ex = hingeRight ? -fz : fz; ez = hingeRight ? fx : -fx; }
        else      { ex = -fx; ez = -fz; }

        float x0 = 0f, x1 = 1f, z0 = 0f, z1 = 1f;

        if (ex > 0) x0 = 1f - DoorThickness;
        else if (ex < 0) x1 = DoorThickness;

        if (ez > 0) z0 = 1f - DoorThickness;
        else if (ez < 0) z1 = DoorThickness;

        min = new Vector3(x0, 0f, z0);
        max = new Vector3(x1, 1f, z1);
    }

    public static void DoorBox(in BlockInfo info, out Vector3 min, out Vector3 max) =>
        DoorBox(info.DoorFacing, info.DoorOpen, info.DoorHingeRight, out min, out max);

    // The same box in world coordinates for the door at block (x, y, z).
    public static void DoorBox(in BlockInfo info, int x, int y, int z, out Vector3 min, out Vector3 max)
    {
        DoorBox(info.DoorFacing, info.DoorOpen, info.DoorHingeRight, out min, out max);
        var origin = new Vector3(x, y, z);
        min += origin;
        max += origin;
    }

    // Ray against an axis-aligned box (slab method). True if the ray enters the box within maxDist.
    public static bool RayHitsBox(Vector3 origin, Vector3 dir, Vector3 min, Vector3 max, float maxDist)
    {
        float tNear = 0f, tFar = maxDist;

        for (int axis = 0; axis < 3; axis++)
        {
            float o = axis == 0 ? origin.X : axis == 1 ? origin.Y : origin.Z;
            float d = axis == 0 ? dir.X : axis == 1 ? dir.Y : dir.Z;
            float lo = axis == 0 ? min.X : axis == 1 ? min.Y : min.Z;
            float hi = axis == 0 ? max.X : axis == 1 ? max.Y : max.Z;

            if (MathF.Abs(d) < 1e-8f)
            {
                if (o < lo || o > hi) return false;
                continue;
            }

            float t0 = (lo - o) / d, t1 = (hi - o) / d;
            if (t0 > t1) (t0, t1) = (t1, t0);

            tNear = MathF.Max(tNear, t0);
            tFar = MathF.Min(tFar, t1);
            if (tNear > tFar) return false;
        }

        return true;
    }
}
