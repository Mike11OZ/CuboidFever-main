using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

public class NoticeModelTests
{
    [Fact]
    public void Nothing_IsShownAtFirst()
    {
        var notice = new NoticeModel();

        Assert.False(notice.IsActive);
        Assert.Equal(0f, notice.Opacity);
    }

    [Fact]
    public void ShownMessage_StaysFullyVisibleThenFadesAndEnds()
    {
        var notice = new NoticeModel();
        notice.Show("Oops", 5f);

        Assert.True(notice.IsActive);
        Assert.Equal(1f, notice.Opacity);

        notice.Update(3f);                       // 2 s left: not fading yet
        Assert.Equal(1f, notice.Opacity);

        notice.Update(1.5f);                     // 0.5 s left: half faded
        Assert.Equal(0.5f, notice.Opacity, 3);

        notice.Update(1f);
        Assert.False(notice.IsActive);
        Assert.Equal(0f, notice.Opacity);
    }

    [Fact]
    public void ANewMessage_ReplacesTheOldOneAndRestartsTheTimer()
    {
        var notice = new NoticeModel();
        notice.Show("first", 2f);
        notice.Update(1.9f);

        notice.Show("second", 2f);

        Assert.Equal("second", notice.Text);
        Assert.Equal(1f, notice.Opacity);
    }

    [Fact]
    public void Dismiss_HidesItAtOnce()
    {
        var notice = new NoticeModel();
        notice.Show("x");

        notice.Dismiss();

        Assert.False(notice.IsActive);
    }

    [Fact]
    public void EmptyMessage_IsNotActive()
    {
        var notice = new NoticeModel();
        notice.Show("");

        Assert.False(notice.IsActive);
    }
}

public class TextWrapTests
{
    // Every character is 1 unit wide
    private static float Width(string s) => s.Length;

    [Fact]
    public void ShortText_IsOneLine()
    {
        Assert.Equal(new[] { "hello world" }, TextWrap.Wrap("hello world", Width, 20, 3));
    }

    [Fact]
    public void LongText_BreaksBetweenWords()
    {
        var lines = TextWrap.Wrap("the quick brown fox jumps", Width, 10, 5);

        Assert.Equal(new[] { "the quick", "brown fox", "jumps" }, lines);
    }

    [Fact]
    public void WordWiderThanTheLine_IsCutIntoPieces()
    {
        var lines = TextWrap.Wrap("abcdefghij", Width, 4, 5);

        Assert.Equal(new[] { "abcd", "efgh", "ij" }, lines);
    }

    [Fact]
    public void TooManyLines_AreCutOffWithDots()
    {
        var lines = TextWrap.Wrap("one two three four five six", Width, 7, 2);

        Assert.Equal(2, lines.Count);
        Assert.EndsWith("...", lines[1]);
        Assert.True(Width(lines[1]) <= 7);
    }

    [Fact]
    public void BlankText_GivesNoLines()
    {
        Assert.Empty(TextWrap.Wrap("   ", Width, 10, 3));
        Assert.Empty(TextWrap.Wrap(null, Width, 10, 3));
    }
}
