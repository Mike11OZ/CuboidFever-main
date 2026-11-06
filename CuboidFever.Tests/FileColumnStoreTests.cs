using System;
using System.IO;
using System.Linq;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

// Each test gets its own temporary world folder, deleted afterwards.
public sealed class FileColumnStoreTests : IDisposable
{
    private readonly string _world = Path.Combine(Path.GetTempPath(), "cuboidfever-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FileColumnStore _store;

    public FileColumnStoreTests() => _store = new FileColumnStore(_world);

    public void Dispose()
    {
        if (Directory.Exists(_world)) Directory.Delete(_world, recursive: true);
    }

    [Fact]
    public void Read_NothingSaved_ReturnsNull()
    {
        Assert.Null(_store.Read(0, 0));
        Assert.False(_store.Exists(0, 0));
    }

    [Fact]
    public void Write_ThenRead_ReturnsSameBytes()
    {
        var data = new byte[] { 1, 2, 3, 250, 0, 7 };

        _store.Write(4, 9, data);

        Assert.True(_store.Exists(4, 9));
        Assert.Equal(data, _store.Read(4, 9));
    }

    [Fact]
    public void Write_NegativeCoordinates_Work()
    {
        _store.Write(-3, -12, new byte[] { 42 });

        Assert.Equal(new byte[] { 42 }, _store.Read(-3, -12));
        Assert.Null(_store.Read(3, 12));
    }

    [Fact]
    public void Write_Twice_KeepsOnlyTheNewData()
    {
        _store.Write(1, 1, new byte[] { 1, 1, 1, 1, 1, 1, 1, 1 });
        _store.Write(1, 1, new byte[] { 9 });

        Assert.Equal(new byte[] { 9 }, _store.Read(1, 1));
    }

    [Fact]
    public void Write_LeavesNoTemporaryFileBehind()
    {
        _store.Write(2, 2, new byte[] { 5 });

        string[] files = Directory.GetFiles(Path.Combine(_world, "columns"));
        Assert.Single(files);
        Assert.EndsWith(".cfc", files[0]);
    }

    [Fact]
    public void Delete_RemovesTheColumn_AndIsSafeWhenMissing()
    {
        _store.Write(6, 6, new byte[] { 1 });

        _store.Delete(6, 6);
        _store.Delete(6, 6);
        _store.Delete(100, 100);

        Assert.False(_store.Exists(6, 6));
        Assert.Null(_store.Read(6, 6));
    }

    [Fact]
    public void ListColumns_ReturnsEverySavedColumn_IgnoringStrayFiles()
    {
        _store.Write(0, 0, new byte[] { 1 });
        _store.Write(-5, 8, new byte[] { 1 });
        _store.Write(12, -1, new byte[] { 1 });

        // Junk that must not be mistaken for a column: an interrupted save, and files that merely look similar
        string columns = Path.Combine(_world, "columns");
        File.WriteAllBytes(Path.Combine(columns, "c.1.1.cfc.tmp"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(columns, "c.x.y.cfc"), new byte[] { 1 });
        File.WriteAllBytes(Path.Combine(columns, "c.1.cfc"), new byte[] { 1 });

        var listed = _store.ListColumns().OrderBy(c => c.cx).ToArray();

        Assert.Equal(new[] { (-5, 8), (0, 0), (12, -1) }, listed);
    }

    [Fact]
    public void ListColumns_NoWorldFolderYet_IsEmpty()
    {
        Assert.Empty(_store.ListColumns());
    }

    [Fact]
    public void Store_HoldsRealSerializedColumns()
    {
        // The two pieces together: serialize a column, keep it in the store, load it back
        var blocks = new byte[Chunk.Size, World.Height, Chunk.Size];
        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < 40; y++)
        for (int z = 0; z < Chunk.Size; z++)
            blocks[x, y, z] = 3;

        var column = new Column(7, -2);
        for (int cy = 0; cy < World.ChunksY; cy++)
        {
            var chunk = new Chunk(7, cy, -2);
            chunk.CopyFromColumn(blocks);
            column.Chunks[cy] = chunk;
        }

        var identity = new byte[256];
        for (int i = 0; i < 256; i++) identity[i] = (byte)i;

        _store.Write(7, -2, ColumnSerializer.Write(column, identity));
        var loaded = ColumnSerializer.Read(_store.Read(7, -2), identity, 7, -2);

        Assert.Equal(3, loaded.GetBlock(5, 39, 5));
        Assert.Equal(Block.Air, loaded.GetBlock(5, 40, 5));
    }
}
