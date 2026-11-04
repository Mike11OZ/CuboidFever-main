using System;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// Water geometry rules, taken from Minecraft Beta 1.7.3 (BlockFluids). Waves are switched off for now, so the
// water surface has no wave displacement. The old wave rules (shore / open / covered / rigid weights and the CPU wave
// surface) are in git history if we bring waves back.
public static class WaterSurface
{
    // Height of a source block with no water above it: 1/9 lower than a full block (see LevelHeight).
    public const float SurfaceHeight = 1f - 1f / 9f;

    // BlockFluids.getPercentAir: how much of the block is empty, for a water level (0 source, 1-7 flowing, 8 falling).
    // A source is (0 + 1) / 9 empty; level 7 is 8/9 empty; falling water counts as a source.
    public static float PercentAir(int fluidLevel)
    {
        if (fluidLevel >= BlockRegistry.WaterFallingLevel) fluidLevel = 0;
        return (fluidLevel + 1) / 9f;
    }

    // Height of the water inside one block (as a fraction of the block) when nothing is above it and nothing around it
    // pulls the surface up or down. Falling water always has water above it, so it is a full block.
    public static float LevelHeight(int fluidLevel) =>
        fluidLevel >= BlockRegistry.WaterFallingLevel ? 1f : 1f - PercentAir(fluidLevel);

    // True when point 'p' (e.g. the camera) is inside the water volume as it is drawn.
    // 'time' is unused until waves return; the CPU surface will need it then.
    //
    // The drawn surface slopes between block corners (see ChunkMesher.FluidCornerHeight); this uses the block's own
    // level height instead, which is within a fraction of a block of it and only matters for the blue tint.
    public static bool IsUnderwater(World world, Vector3 p, float time = 0f)
    {
        int x = (int)MathF.Floor(p.X);
        int y = (int)MathF.Floor(p.Y);
        int z = (int)MathF.Floor(p.Z);

        var info = BlockRegistry.Instance.Info;
        var here = info[world.GetBlock(x, y, z)];
        if (!here.IsWater) return false;

        // More water on top: the whole cell is water. Otherwise only up to this level's surface.
        return info[world.GetBlock(x, y + 1, z)].IsWater || p.Y < y + LevelHeight(here.FluidLevel);
    }
}
