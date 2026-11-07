using System;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

// What a column needs after it is loaded from disk: its lamp light rebuilt, and its water and sand restarted.
// The block data is passed in by hand (BlockInfo), because the real BlockRegistry needs the texture atlas.
public class ColumnLoadStateTests
{
    private const byte Air = 0, Stone = 1, Lamp = 2, Source = 3, Flowing = 4, Sand = 5;

    private static BlockInfo[] Infos()
    {
        var infos = new BlockInfo[256];
        infos[Air] = new BlockInfo { Transparent = true, IsAir = true, FluidLevel = -1 };
        infos[Stone] = new BlockInfo { Solid = true, FluidLevel = -1 };
        infos[Lamp] = new BlockInfo { Solid = true, Emissive = true, FluidLevel = -1 };
        infos[Source] = new BlockInfo { Transparent = true, IsWater = true, Tick = TickKind.Water, FluidLevel = 0 };
        infos[Flowing] = new BlockInfo { Transparent = true, IsWater = true, Tick = TickKind.Water, FluidLevel = 3 };
        infos[Sand] = new BlockInfo { Solid = true, Tick = TickKind.Gravity, FluidLevel = -1 };
        return infos;
    }

    private static Column EmptyColumn(int cx, int cz)
    {
        var column = new Column(cx, cz);
        for (int cy = 0; cy < World.ChunksY; cy++)
            column.Chunks[cy] = new Chunk(cx, cy, cz);
        return column;
    }

    private static void Set(Column column, int x, int y, int z, byte id) =>
        column.Chunks[y >> 4].SetBlock(x, y & 15, z, id);

    private static int LightAt(Column column, int channel, int x, int y, int z) =>
        column.Light[channel][Column.LIdx(x, y, z)];

    // =====================  LAMP LIGHT  =====================

    [Fact]
    public void Lamp_LightsItselfAndFadesByOnePerBlock()
    {
        var column = EmptyColumn(0, 0);
        Set(column, 8, 10, 8, Lamp);

        column.ComputeBlockLight(Infos(), id => id == Lamp ? (15, 0, 0) : (0, 0, 0));

        Assert.Equal(15, LightAt(column, 1, 8, 10, 8));
        Assert.Equal(14, LightAt(column, 1, 9, 10, 8));
        Assert.Equal(13, LightAt(column, 1, 8, 12, 8));
        Assert.Equal(8, LightAt(column, 1, 15, 10, 8));   // seven blocks away
    }

    [Fact]
    public void Lamp_LightsEachColorChannelSeparately()
    {
        var column = EmptyColumn(0, 0);
        Set(column, 8, 10, 8, Lamp);

        column.ComputeBlockLight(Infos(), id => id == Lamp ? (0, 15, 10) : (0, 0, 0));

        Assert.Equal(0, LightAt(column, 1, 8, 10, 8));
        Assert.Equal(15, LightAt(column, 2, 8, 10, 8));
        Assert.Equal(10, LightAt(column, 3, 8, 10, 8));
        Assert.Equal(9, LightAt(column, 3, 9, 10, 8));
    }

    [Fact]
    public void Lamp_LightGoesAroundWalls()
    {
        var column = EmptyColumn(0, 0);
        Set(column, 8, 10, 8, Lamp);
        Set(column, 9, 10, 8, Stone);

        column.ComputeBlockLight(Infos(), id => id == Lamp ? (15, 0, 0) : (0, 0, 0));

        Assert.Equal(0, LightAt(column, 1, 9, 10, 8));     // inside the wall
        Assert.Equal(11, LightAt(column, 1, 10, 10, 8));   // behind it: four steps around, not two through
    }

    [Fact]
    public void NoLamps_NoBlockLight()
    {
        var column = EmptyColumn(0, 0);
        Set(column, 3, 3, 3, Stone);

        column.ComputeBlockLight(Infos(), id => (0, 0, 0));

        for (int channel = 1; channel <= 3; channel++)
            Assert.Equal(0, LightAt(column, channel, 3, 3, 3));
    }

    // =====================  WATER AND SAND  =====================

    [Fact]
    public void FlowingWater_AndUnsupportedSand_AreWokenWithWorldCoordinates()
    {
        var column = EmptyColumn(2, -1);

        Set(column, 1, 5, 1, Source);      // a source block is stable: left alone
        Set(column, 2, 5, 2, Flowing);     // flowing water: woken
        Set(column, 3, 20, 3, Sand);       // sand in mid-air: woken
        Set(column, 4, 19, 4, Stone);
        Set(column, 4, 20, 4, Sand);       // sand resting on stone: left alone
        Set(column, 5, 0, 5, Sand);        // sand on the bottom of the world: left alone

        var wake = column.FindBlocksToWake(Infos());

        Assert.Equal(2, wake.Count);
        Assert.Contains((34, 5, -14), wake);    // column (2, -1): x = 2 * 16 + 2, z = -16 + 2
        Assert.Contains((35, 20, -13), wake);
    }

    [Fact]
    public void ColumnWithNothingToWake_ReturnsNothing()
    {
        var column = EmptyColumn(0, 0);
        Set(column, 1, 1, 1, Stone);

        Assert.Empty(column.FindBlocksToWake(Infos()));
    }
}
