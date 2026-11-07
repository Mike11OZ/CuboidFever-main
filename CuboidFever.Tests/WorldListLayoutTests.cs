using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

public class WorldListLayoutTests
{
    [Fact]
    public void PageCount_RoundsUp_AndIsNeverZero()
    {
        Assert.Equal(1, WorldListLayout.PageCount(0));
        Assert.Equal(1, WorldListLayout.PageCount(1));
        Assert.Equal(1, WorldListLayout.PageCount(WorldListLayout.PageSize));
        Assert.Equal(2, WorldListLayout.PageCount(WorldListLayout.PageSize + 1));
        Assert.Equal(2, WorldListLayout.PageCount(WorldListLayout.PageSize * 2));
        Assert.Equal(3, WorldListLayout.PageCount(WorldListLayout.PageSize * 2 + 1));
    }

    [Fact]
    public void PageRange_GivesEachPageItsSlice()
    {
        // With 10 worlds and 4 per page: 0-3, 4-7, 8-9
        Assert.Equal((0, 4), WorldListLayout.PageRange(10, 0));
        Assert.Equal((4, 4), WorldListLayout.PageRange(10, 1));
        Assert.Equal((8, 2), WorldListLayout.PageRange(10, 2));
    }

    [Fact]
    public void PageRange_ClampsAPageThatDoesNotExist()
    {
        // e.g. the last world on the last page was deleted, so the page the menu remembered is gone
        Assert.Equal((8, 2), WorldListLayout.PageRange(10, 7));
        Assert.Equal((0, 4), WorldListLayout.PageRange(10, -3));
    }

    [Fact]
    public void PageRange_EmptyList_HasNothingToShow()
    {
        Assert.Equal((0, 0), WorldListLayout.PageRange(0, 0));
        Assert.Equal((0, 0), WorldListLayout.PageRange(0, 5));
    }

    [Fact]
    public void Shorten_LeavesShortNamesAlone()
    {
        Assert.Equal("World 1", WorldListLayout.Shorten("World 1", 26));
        Assert.Equal("World 1", WorldListLayout.Shorten("  World 1  ", 26));
    }

    [Fact]
    public void Shorten_CutsLongNamesToTheLimit()
    {
        string shortened = WorldListLayout.Shorten(new string('x', 40), 26);

        Assert.Equal(26, shortened.Length);
        Assert.True(shortened.EndsWith("..."), shortened);
    }

    [Fact]
    public void Shorten_BlankNameIsLabeled()
    {
        Assert.Equal("(unnamed)", WorldListLayout.Shorten("", 26));
        Assert.Equal("(unnamed)", WorldListLayout.Shorten("   ", 26));
        Assert.Equal("(unnamed)", WorldListLayout.Shorten(null, 26));
    }
}
