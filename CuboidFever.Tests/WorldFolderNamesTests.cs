using System;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

public class WorldFolderNamesTests
{
    private static string Make(string name, params string[] taken) =>
        WorldFolderNames.Make(name, candidate => Array.IndexOf(taken, candidate) >= 0);

    [Fact]
    public void OrdinaryName_IsKeptAsIs()
    {
        Assert.Equal("My World", Make("My World"));
    }

    [Fact]
    public void CharactersFilesCannotContain_AreReplaced()
    {
        Assert.Equal("a_b", Make("a/b"));
    }

    [Fact]
    public void BlankName_BecomesWorld()
    {
        Assert.Equal("World", Make("   "));
        Assert.Equal("World", Make(""));
        Assert.Equal("World", Make(null));
    }

    [Fact]
    public void TrailingDotsAndSpaces_AreRemoved()
    {
        Assert.Equal("abc", Make("abc. "));
        Assert.Equal("abc", Make("abc..."));
    }

    [Fact]
    public void NamesWindowsReserves_GetAnUnderscore()
    {
        Assert.Equal("CON_", Make("CON"));
        Assert.Equal("nul.txt_", Make("nul.txt"));
        Assert.Equal("Lpt1_", Make("Lpt1"));
        Assert.Equal("Console", Make("Console"));
    }

    [Fact]
    public void VeryLongName_IsShortened()
    {
        Assert.Equal(60, Make(new string('x', 100)).Length);
    }

    [Fact]
    public void NameAlreadyTaken_GetsANumber()
    {
        Assert.Equal("World (2)", Make("World", "World"));
        Assert.Equal("World (3)", Make("World", "World", "World (2)"));
        Assert.Equal("World", Make("World", "World (2)"));
    }
}
