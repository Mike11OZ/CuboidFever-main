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
