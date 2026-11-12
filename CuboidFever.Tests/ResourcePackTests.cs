using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

// Each test gets its own temporary resourcepacks folder. The texture contents here are plain text: the pack
// code only reads files as bytes, and decoding images needs a graphics device (that part is TextureAtlas.ApplyPack).
public sealed class ResourcePackTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cuboidfever-tests-" + Guid.NewGuid().ToString("N"));

    public ResourcePackTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private const string Block = "assets/cuboidfever/textures/block/";

    private string MakeFolderPack(string name, params (string Path, string Content)[] files)
    {
        string folder = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.Combine(folder, "assets", "cuboidfever", "textures"));

        foreach (var (path, content) in files)
        {
            string full = Path.Combine(folder, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, content);
        }

        return folder;
    }

    private string MakeZipPack(string fileName, params (string Path, string Content)[] files)
    {
        string file = Path.Combine(_root, fileName);

        using (ZipArchive zip = ZipFile.Open(file, ZipArchiveMode.Create))
        {
            foreach (var (path, content) in files)
            {
                ZipArchiveEntry entry = zip.CreateEntry(path);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        return file;
    }

    [Fact]
    public void Discover_FindsFolderAndZipPacks_SortedByName()
    {
        MakeFolderPack("Beta", (Block + "stone.png", "x"));
        MakeZipPack("alpha.zip", (Block + "stone.png", "x"));

        var packs = ResourcePackInfo.Discover(_root);

        Assert.Equal(new[] { "alpha.zip", "Beta" }, packs.Select(p => p.Id).ToArray());
        Assert.Equal("alpha", packs[0].DisplayName);
        Assert.True(packs[0].IsZip);
        Assert.Equal("Beta", packs[1].DisplayName);
        Assert.False(packs[1].IsZip);
    }

    [Fact]
    public void Discover_SkipsAnythingThatIsNotAPack()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Empty Folder"));
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "hello");
        MakeZipPack("unrelated.zip", ("readme.txt", "hello"));
        File.WriteAllText(Path.Combine(_root, "broken.zip"), "this is not a zip file");

        Assert.Empty(ResourcePackInfo.Discover(_root));
    }

    [Fact]
    public void Discover_MissingFolder_GivesNoPacks()
    {
        Assert.Empty(ResourcePackInfo.Discover(Path.Combine(_root, "does-not-exist")));
    }

    [Fact]
    public void ReadBlockTextures_Folder_KeysAreNamesWithoutExtension_AndOnlyBlockPngsAreRead()
    {
        string folder = MakeFolderPack("Pack",
            (Block + "stone.png", "stone-data"),
            (Block + "sub/lamp.png", "lamp-data"),
            (Block + "notes.txt", "not a texture"),
            ("assets/cuboidfever/textures/gui/menu.png", "not a block texture"));

        var textures = ResourcePackInfo.FromFolder(folder).ReadBlockTextures();

        Assert.Equal(new[] { "stone", "sub/lamp" }, textures.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        Assert.Equal(Encoding.UTF8.GetBytes("stone-data"), textures["stone"]);
        Assert.Equal(Encoding.UTF8.GetBytes("lamp-data"), textures["sub/lamp"]);
    }

    [Fact]
    public void ReadBlockTextures_Zip_GivesTheSameResultAsAFolder()
    {
        string zip = MakeZipPack("Pack.zip",
            (Block + "stone.png", "stone-data"),
            (Block + "sub/", ""), // a folder entry
            (Block + "sub/lamp.png", "lamp-data"),
            (Block + "notes.txt", "not a texture"),
            ("assets/cuboidfever/textures/gui/menu.png", "not a block texture"));

        var textures = ResourcePackInfo.FromZip(zip).ReadBlockTextures();

        Assert.Equal(new[] { "stone", "sub/lamp" }, textures.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        Assert.Equal(Encoding.UTF8.GetBytes("stone-data"), textures["stone"]);
        Assert.Equal(Encoding.UTF8.GetBytes("lamp-data"), textures["sub/lamp"]);
    }

    [Fact]
    public void ReadBlockTextures_KeysIgnoreCase_LikeTheAtlasNames()
    {
        string folder = MakeFolderPack("Pack", (Block + "stone.png", "x"));

        var textures = ResourcePackInfo.FromFolder(folder).ReadBlockTextures();

        Assert.True(textures.ContainsKey("STONE"));
    }

    [Fact]
    public void ZipPack_IsNotLeftOpen_SoItCanBeDeletedAfterwards()
    {
        string zip = MakeZipPack("Pack.zip", (Block + "stone.png", "x"));

        ResourcePackInfo.Discover(_root);
        ResourcePackInfo.FromZip(zip).ReadBlockTextures();

        File.Delete(zip); // would throw on Windows if the game still held the file open
        Assert.False(File.Exists(zip));
    }

    // The first 24 bytes of a PNG: signature, then the IHDR chunk's length, type, width and height. That is all
    // the pack picture check looks at, so a real image is not needed.
    private static byte[] PngHeader(int width, int height, int extraBytes = 0)
    {
        var bytes = new byte[24 + extraBytes];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        bytes[7 + 4] = 13;                                  // IHDR length (big endian, low byte)
        Encoding.ASCII.GetBytes("IHDR").CopyTo(bytes, 12);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16, 4), width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20, 4), height);
        return bytes;
    }

    [Fact]
    public void Thumbnail_FolderPackPicture_IsRead()
    {
        string folder = MakeFolderPack("Pack");
        byte[] png = PngHeader(64, 64);
        File.WriteAllBytes(Path.Combine(folder, "pack.png"), png);

        Assert.Equal(png, ResourcePackInfo.FromFolder(folder).TryReadThumbnail());
    }

    [Fact]
    public void Thumbnail_ZipPackPicture_IsRead_OnlyFromTheTopOfTheZip()
    {
        byte[] top = PngHeader(64, 64);
        byte[] nested = PngHeader(32, 32);

        string withTop = Path.Combine(_root, "Top.zip");
        using (ZipArchive zip = ZipFile.Open(withTop, ZipArchiveMode.Create))
        {
            zip.CreateEntry(Block + "stone.png").Open().Dispose();
            using Stream s = zip.CreateEntry("pack.png").Open();
            s.Write(top);
        }

        string onlyNested = Path.Combine(_root, "Nested.zip");
        using (ZipArchive zip = ZipFile.Open(onlyNested, ZipArchiveMode.Create))
        {
            zip.CreateEntry(Block + "stone.png").Open().Dispose();
            using Stream s = zip.CreateEntry("assets/pack.png").Open();
            s.Write(nested);
        }

        Assert.Equal(top, ResourcePackInfo.FromZip(withTop).TryReadThumbnail());
        Assert.Null(ResourcePackInfo.FromZip(onlyNested).TryReadThumbnail());
    }

    [Fact]
    public void Thumbnail_MissingOrUnusablePicture_GivesNull()
    {
        // none at all
        Assert.Null(ResourcePackInfo.FromFolder(MakeFolderPack("None")).TryReadThumbnail());

        // not a PNG
        string notPng = MakeFolderPack("NotPng", ("pack.png", "this is text, not a picture at all"));
        Assert.Null(ResourcePackInfo.FromFolder(notPng).TryReadThumbnail());

        // empty file
        string empty = MakeFolderPack("Empty", ("pack.png", ""));
        Assert.Null(ResourcePackInfo.FromFolder(empty).TryReadThumbnail());

        // a zip with no picture
        Assert.Null(ResourcePackInfo.FromZip(MakeZipPack("NoPic.zip", (Block + "stone.png", "x"))).TryReadThumbnail());
    }

    [Fact]
    public void Thumbnail_HeaderCheck_AcceptsUpToTheLimit_AndRejectsBeyondIt()
    {
        int max = ResourcePackInfo.MaxThumbnailSize;

        Assert.True(ResourcePackInfo.IsUsableThumbnail(PngHeader(1, 1)));
        Assert.True(ResourcePackInfo.IsUsableThumbnail(PngHeader(max, max)));
        Assert.False(ResourcePackInfo.IsUsableThumbnail(PngHeader(max + 1, 64)));
        Assert.False(ResourcePackInfo.IsUsableThumbnail(PngHeader(64, max + 1)));
        Assert.False(ResourcePackInfo.IsUsableThumbnail(PngHeader(0, 64)));
        Assert.False(ResourcePackInfo.IsUsableThumbnail(PngHeader(-5, 64)));
    }

    [Fact]
    public void Thumbnail_HeaderCheck_RejectsDamagedHeaders()
    {
        Assert.False(ResourcePackInfo.IsUsableThumbnail(null));
        Assert.False(ResourcePackInfo.IsUsableThumbnail(new byte[10]));      // too short
        Assert.False(ResourcePackInfo.IsUsableThumbnail(new byte[24]));      // no signature

        byte[] noIhdr = PngHeader(64, 64);
        noIhdr[12] = (byte)'X';                                              // chunk is not IHDR
        Assert.False(ResourcePackInfo.IsUsableThumbnail(noIhdr));
    }

    [Fact]
    public void FitInside_SquarePictureInWideBox_IsCenteredAndKeepsItsShape()
    {
        // a 64x64 picture in a 128x72 box: as tall as the box, centered sideways
        var (x, y, w, h) = WorldListLayout.FitInside(10, 20, 128, 72, 64, 64);

        Assert.Equal((72, 72), (w, h));
        Assert.Equal(10 + (128 - 72) / 2, x);
        Assert.Equal(20, y);
    }

    [Fact]
    public void FitInside_WidePictureInTallBox_IsCenteredVertically()
    {
        var (x, y, w, h) = WorldListLayout.FitInside(0, 0, 100, 100, 200, 100);

        Assert.Equal((100, 50), (w, h));
        Assert.Equal(0, x);
        Assert.Equal(25, y);
    }

    [Fact]
    public void FitInside_PictureShapedLikeTheBox_FillsIt_AndSmallPicturesAreEnlarged()
    {
        Assert.Equal((5, 6, 160, 90), WorldListLayout.FitInside(5, 6, 160, 90, 320, 180));
        Assert.Equal((0, 0, 160, 90), WorldListLayout.FitInside(0, 0, 160, 90, 16, 9));
    }

    [Fact]
    public void FitInside_NeverLeavesTheBox_EvenForExtremeShapes()
    {
        foreach (var (pw, ph) in new[] { (1, 1000), (1000, 1), (1, 1), (1024, 1024) })
        {
            var (x, y, w, h) = WorldListLayout.FitInside(0, 0, 128, 72, pw, ph);

            Assert.InRange(w, 1, 128);
            Assert.InRange(h, 1, 72);
            Assert.InRange(x, 0, 128 - w);
            Assert.InRange(y, 0, 72 - h);
        }
    }

    [Fact]
    public void FitInside_DegenerateSizes_DoNotThrow()
    {
        Assert.Equal((3, 4, 100, 50), WorldListLayout.FitInside(3, 4, 100, 50, 0, 0)); // unknown picture size fills the box
        Assert.Equal((3, 4, 0, 0), WorldListLayout.FitInside(3, 4, 0, 0, 64, 64));    // no room
    }

    [Fact]
    public void Options_SavedPackIsReadBack_AndTheFolderIsCreated()
    {
        string path = Path.Combine(_root, "nested", "options.json");

        new GameOptions { ResourcePack = "My Pack.zip" }.Save(path);

        Assert.Equal("My Pack.zip", GameOptions.Load(path).ResourcePack);
    }

    [Fact]
    public void Options_MissingOrDamagedFile_GivesDefaults()
    {
        string missing = Path.Combine(_root, "missing.json");
        Assert.Null(GameOptions.Load(missing).ResourcePack);

        string damaged = Path.Combine(_root, "damaged.json");
        File.WriteAllText(damaged, "{ this is not json");
        Assert.Null(GameOptions.Load(damaged).ResourcePack);
    }
}
