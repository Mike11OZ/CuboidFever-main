using System;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// Water geometry rules, taken from Minecraft Beta 1.7.3 (BlockFluids). Waves are switched off for now, so the
// water surface is flat. The old wave rules (shore / open / covered / rigid weights and the CPU wave surface)
// are in git history if we bring waves back.
public static class WaterSurface
{
    // A source block with no water above it stands 1/9 lower than a full block (BlockFluids.d(): (meta + 1) / 9,
    // meta 0 for a source). With water above, the block is full height.
    public const float SurfaceHeight = 1f - 1f / 9f;

    // True when point 'p' (e.g. the camera) is inside the water volume as it is drawn.
    // 'time' is unused until waves return; the CPU surface will need it then.
    public static bool IsUnderwater(World world, Vector3 p, float time = 0f)
    {
        int x = (int)MathF.Floor(p.X);
        int y = (int)MathF.Floor(p.Y);
        int z = (int)MathF.Floor(p.Z);

        var info = BlockRegistry.Instance.Info;
        if (!info[world.GetBlock(x, y, z)].IsWater) return false;

        // More water on top: the whole cell is water. Otherwise only up to the lowered surface.
        return info[world.GetBlock(x, y + 1, z)].IsWater || p.Y < y + SurfaceHeight;
    }
}
