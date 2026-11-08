using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

// The world list's layout and scrolling arithmetic (no graphics needed).
public class WorldListLayoutTests
{
    // An 800x480 window at scale 1: header 72, footer 64, so the viewport is 344 tall. Rows are 72 + 8 gap.
    private static ListMetrics Small => WorldListLayout.Metrics(800, 480, 1);

    [Fact]
    public void Viewport_FillsTheSpaceBetweenHeaderAndFooter()
    {
        var m = Small;

        Assert.Equal(72, m.Top);
        Assert.Equal(344, m.Height);
        Assert.Equal(480 - 64, m.Bottom);
    }

    [Fact]
    public void Rows_AreCenteredAndFitNarrowWindows()
    {
        var wide = Small;
        Assert.Equal((800 - wide.Width) / 2, wide.Left);

        var narrow = WorldListLayout.Metrics(300, 480, 1);
        Assert.True(narrow.Left >= WorldListLayout.ScreenMargin);
        Assert.True(narrow.ScrollbarLeft + narrow.ScrollbarWidth <= 300 - WorldListLayout.ScreenMargin);
    }

    [Fact]
    public void ScaleMultipliesEverything()
    {
        var m = WorldListLayout.Metrics(1920, 1080, 2);

        Assert.Equal(144, m.RowHeight);
        Assert.Equal(16, m.RowGap);
        Assert.Equal(144, m.Top);
    }

    [Fact]
    public void ContentHeight_CountsRowsAndGaps()
    {
        var m = Small;

        Assert.Equal(0, WorldListLayout.ContentHeight(0, m));
        Assert.Equal(72, WorldListLayout.ContentHeight(1, m));
        Assert.Equal(72 * 3 + 8 * 2, WorldListLayout.ContentHeight(3, m));
    }

    [Fact]
    public void FewWorlds_CannotScroll()
    {
        var m = Small; // 4 rows = 4*72 + 3*8 = 312, which fits in 344

        Assert.Equal(0, WorldListLayout.MaxScroll(4, m));
        Assert.Equal(0, WorldListLayout.ClampScroll(100, 4, m));
        Assert.Null(WorldListLayout.Thumb(4, 0, m, 16));
    }

    [Fact]
    public void ManyWorlds_ScrollExactlyToTheLastRow()
    {
        var m = Small;
        int content = WorldListLayout.ContentHeight(50, m);

        Assert.Equal(content - 344, WorldListLayout.MaxScroll(50, m));
        Assert.Equal(WorldListLayout.MaxScroll(50, m), WorldListLayout.ClampScroll(999999, 50, m));
        Assert.Equal(0, WorldListLayout.ClampScroll(-30, 50, m));
    }

    [Fact]
    public void RowAt_FindsTheRowUnderTheMouse()
    {
        var m = Small;
        int x = m.Left + 10;

        Assert.Equal(0, WorldListLayout.RowAt(x, m.Top + 5, 0, 10, m));
        Assert.Equal(1, WorldListLayout.RowAt(x, m.Top + 80 + 5, 0, 10, m));

        // Scrolled down one full row stride, the second row is now at the top
        Assert.Equal(1, WorldListLayout.RowAt(x, m.Top + 5, 80, 10, m));
    }

    [Fact]
    public void RowAt_IgnoresGapsAndOutsideTheViewport()
    {
        var m = Small;
        int x = m.Left + 10;

        Assert.Equal(-1, WorldListLayout.RowAt(x, m.Top + 75, 0, 10, m));      // gap between rows 0 and 1
        Assert.Equal(-1, WorldListLayout.RowAt(x, m.Top - 1, 0, 10, m));       // in the header
        Assert.Equal(-1, WorldListLayout.RowAt(x, m.Bottom, 0, 10, m));        // in the footer
        Assert.Equal(-1, WorldListLayout.RowAt(m.Left - 1, m.Top + 5, 0, 10, m));
        Assert.Equal(-1, WorldListLayout.RowAt(m.Left + m.Width, m.Top + 5, 0, 10, m));
    }

    [Fact]
    public void RowAt_BelowTheLastRow_IsNothing()
    {
        var m = Small;

        Assert.Equal(-1, WorldListLayout.RowAt(m.Left + 5, m.Top + 2 * 80 + 5, 0, 2, m));
    }

    [Fact]
    public void VisibleRows_CoverTheViewport()
    {
        var m = Small;

        Assert.Equal((0, 5), WorldListLayout.VisibleRows(0, 50, m));   // 344 px shows rows 0-4 (the 5th partly)
        Assert.Equal((0, 0), WorldListLayout.VisibleRows(0, 0, m));
        Assert.Equal((0, 2), WorldListLayout.VisibleRows(0, 2, m));

        var (first, count) = WorldListLayout.VisibleRows(WorldListLayout.MaxScroll(50, m), 50, m);
        Assert.Equal(49, first + count - 1);
    }

    [Fact]
    public void Thumb_FillsTheTrackAtTheTopAndBottom()
    {
        var m = Small;
        int max = WorldListLayout.MaxScroll(50, m);

        var top = WorldListLayout.Thumb(50, 0, m, 16).Value;
        var bottom = WorldListLayout.Thumb(50, max, m, 16).Value;

        Assert.Equal(0, top.offset);
        Assert.Equal(m.Height, bottom.offset + bottom.length);
        Assert.Equal(top.length, bottom.length);
    }

    [Fact]
    public void Thumb_NeverShorterThanTheMinimum()
    {
        var m = Small;

        var thumb = WorldListLayout.Thumb(5000, 0, m, 24).Value;

        Assert.Equal(24, thumb.length);
    }

    [Fact]
    public void DraggingTheThumb_MapsBackToScroll()
    {
        var m = Small;
        int max = WorldListLayout.MaxScroll(50, m);

        Assert.Equal(0, WorldListLayout.ScrollForThumbCenter(m.Top - 100, 50, m, 16));
        Assert.Equal(max, WorldListLayout.ScrollForThumbCenter(m.Bottom + 100, 50, m, 16));

        int middle = WorldListLayout.ScrollForThumbCenter(m.Top + m.Height / 2, 50, m, 16);
        Assert.True(middle > 0 && middle < max);
    }

    [Fact]
    public void DeleteButton_SitsInsideTheRowOnTheRight()
    {
        var (x, y, w, h) = WorldListLayout.DeleteButtonRect(100, 200, 440, 72, 1);

        Assert.True(x > 100 + 440 / 2);          // right half of the row
        Assert.True(x + w < 100 + 440);          // inside the right edge
        Assert.Equal(200 + (72 - h) / 2, y);     // centered vertically
    }

    [Fact]
    public void DeleteButton_IsWideEnoughForItsLabel()
    {
        var plain = WorldListLayout.DeleteButtonRect(0, 0, 440, 72, 1);
        var wide = WorldListLayout.DeleteButtonRect(0, 0, 440, 72, 1, labelWidth: 100);

        Assert.True(wide.width >= 100 + 16);
        Assert.True(wide.width > plain.width);
        Assert.True(wide.x + wide.width < 440); // still inside the row
    }

    [Fact]
    public void DeleteButton_GrowsWithTheScale()
    {
        var one = WorldListLayout.DeleteButtonRect(0, 0, 440, 72, 1);
        var two = WorldListLayout.DeleteButtonRect(0, 0, 880, 144, 2);

        Assert.Equal(one.width * 2, two.width);
        Assert.Equal(one.height * 2, two.height);
    }

    [Fact]
    public void DisplayName_TrimsAndLabelsBlankNames()
    {
        Assert.Equal("My World", WorldListLayout.DisplayName("  My World "));
        Assert.Equal("(unnamed)", WorldListLayout.DisplayName("   "));
        Assert.Equal("(unnamed)", WorldListLayout.DisplayName(null));
    }
}
