using System;
using System.IO;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

// These tests use hand-made translation tables instead of a BlockRegistry, because the registry needs the
// texture atlas (and so a graphics device) to start up.
public class ColumnSerializerTests
{
    private const int Stone = 3, Dirt = 4, Grass = 5, Water = 9;

    private static byte[] Identity()
    {
        var table = new byte[256];
        for (int i = 0; i < 256; i++) table[i] = (byte)i;
        return table;
    }

    // A column built the way world generation builds one: fill a full-height array, then copy each chunk out of it.
    private static Column MakeColumn(int cx, int cz, Func<int, int, int, byte> blockAt)
    {
        var blocks = new byte[Chunk.Size, World.Height, Chunk.Size];
        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < World.Height; y++)
        for (int z = 0; z < Chunk.Size; z++)
            blocks[x, y, z] = blockAt(x, y, z);

        var column = new Column(cx, cz);
        for (int cy = 0; cy < World.ChunksY; cy++)
        {
            var chunk = new Chunk(cx, cy, cz);
            chunk.CopyFromColumn(blocks);
            column.Chunks[cy] = chunk;
        }
        return column;
    }

    // Bumpy terrain with a few layers of its own, so chunks end up uniform, partly uniform and fully mixed
    private static Column MakeTerrain(int cx, int cz) => MakeColumn(cx, cz, (x, y, z) =>
    {
        int surface = 40 + ((x * 7 + z * 13) % 9);
        if (y > surface) return y < 50 ? (byte)Water : Block.Air;
        if (y == surface) return Grass;
        if (y > surface - 4) return Dirt;
        return Stone;
    });

    private static void AssertSameBlocks(Column expected, Column actual)
    {
        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < World.Height; y++)
        for (int z = 0; z < Chunk.Size; z++)
            Assert.True(expected.GetBlock(x, y, z) == actual.GetBlock(x, y, z),
                $"Block differs at ({x}, {y}, {z}): expected {expected.GetBlock(x, y, z)}, got {actual.GetBlock(x, y, z)}");
    }

    [Fact]
    public void RoundTrip_MixedTerrain_KeepsEveryBlock()
    {
        var original = MakeTerrain(5, -3);

        byte[] bytes = ColumnSerializer.Write(original, Identity());
        var loaded = ColumnSerializer.Read(bytes, Identity(), 5, -3);

        Assert.Equal(5, loaded.CX);
        Assert.Equal(-3, loaded.CZ);
        AssertSameBlocks(original, loaded);
    }

    [Fact]
    public void RoundTrip_AllOneBlock_IsTiny()
    {
        var original = MakeColumn(0, 0, (x, y, z) => Stone);

        byte[] bytes = ColumnSerializer.Write(original, Identity());
        var loaded = ColumnSerializer.Read(bytes, Identity(), 0, 0);

        AssertSameBlocks(original, loaded);
        Assert.True(bytes.Length < 100, $"A column of one block type should be tiny, was {bytes.Length} bytes.");
    }

    [Fact]
    public void RoundTrip_AfterEdits_KeepsEdits()
    {
        var original = MakeTerrain(1, 1);
        original.Chunks[0].SetBlock(2, 3, 4, Dirt);
        original.Chunks[2].SetBlock(15, 15, 15, Water);
        original.Chunks[7].SetBlock(0, 0, 0, Stone);

        var loaded = ColumnSerializer.Read(ColumnSerializer.Write(original, Identity()), Identity(), 1, 1);

        AssertSameBlocks(original, loaded);
    }

    [Fact]
    public void Tables_TranslateBlockIdsBothWays()
    {
        // The "saved" world numbers stone as 100 and dirt as 101; this build numbers them 3 and 4
        var toSaved = Identity();
        toSaved[Stone] = 100;
        toSaved[Dirt] = 101;

        var toRuntime = Identity();
        toRuntime[100] = Stone;
        toRuntime[101] = Dirt;

        var original = MakeTerrain(0, 0);
        byte[] bytes = ColumnSerializer.Write(original, toSaved);

        // Reading with a plain identity table shows what was actually stored
        var asStored = ColumnSerializer.Read(bytes, Identity(), 0, 0);
        Assert.Equal(100, asStored.GetBlock(0, 0, 0));   // deep underground: stone

        // Reading with the proper table gets the original blocks back
        var loaded = ColumnSerializer.Read(bytes, toRuntime, 0, 0);
        AssertSameBlocks(original, loaded);
    }

    [Fact]
    public void Read_WrongColumn_Throws()
    {
        byte[] bytes = ColumnSerializer.Write(MakeTerrain(4, 4), Identity());

        Assert.Throws<InvalidDataException>(() => ColumnSerializer.Read(bytes, Identity(), 4, 5));
        Assert.Throws<InvalidDataException>(() => ColumnSerializer.Read(bytes, Identity(), 9, 4));
    }

    [Fact]
    public void Read_BadMagic_Throws()
    {
        byte[] bytes = ColumnSerializer.Write(MakeTerrain(0, 0), Identity());
        bytes[0] = 0;

        Assert.Throws<InvalidDataException>(() => ColumnSerializer.Read(bytes, Identity(), 0, 0));
    }

    [Fact]
    public void Read_NewerFormatVersion_Throws()
    {
        byte[] bytes = ColumnSerializer.Write(MakeTerrain(0, 0), Identity());
        bytes[4] = (byte)(ColumnSerializer.FormatVersion + 1);

        var error = Assert.Throws<InvalidDataException>(() => ColumnSerializer.Read(bytes, Identity(), 0, 0));
        Assert.Contains("newer", error.Message);
    }

    [Fact]
    public void Read_Truncated_Throws()
    {
        byte[] bytes = ColumnSerializer.Write(MakeTerrain(0, 0), Identity());

        Assert.Throws<InvalidDataException>(() => ColumnSerializer.Read(bytes[..10], Identity(), 0, 0));
        Assert.Throws<InvalidDataException>(() => ColumnSerializer.Read(bytes[..(bytes.Length / 2)], Identity(), 0, 0));
    }

    [Fact]
    public void Read_DamagedData_Throws()
    {
        byte[] bytes = ColumnSerializer.Write(MakeTerrain(0, 0), Identity());

        int middle = 21 + (bytes.Length - 21) / 2;   // inside the compressed payload, past the 21-byte header
        bytes[middle] ^= 0xFF;

        Assert.Throws<InvalidDataException>(() => ColumnSerializer.Read(bytes, Identity(), 0, 0));
    }

    [Fact]
    public void Tables_MustHave256Entries()
    {
        var column = MakeTerrain(0, 0);

        Assert.Throws<ArgumentException>(() => ColumnSerializer.Write(column, new byte[10]));
        Assert.Throws<ArgumentException>(() => ColumnSerializer.Read(new byte[100], new byte[10], 0, 0));
    }
}
