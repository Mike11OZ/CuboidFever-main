using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

public class TextFieldModelTests
{
    [Fact]
    public void TypingAddsToTheEnd()
    {
        var field = new TextFieldModel(10);

        field.Type('a');
        field.Type('b');

        Assert.Equal("ab", field.Text);
    }

    [Fact]
    public void TypingStopsAtTheMaximumLength()
    {
        var field = new TextFieldModel(3);

        foreach (char c in "abcdef") field.Type(c);

        Assert.Equal("abc", field.Text);
        Assert.False(field.Type('x'));
    }

    [Fact]
    public void Backspace_RemovesTheLastCharacter_AndDoesNothingWhenEmpty()
    {
        var field = new TextFieldModel(10);
        field.Type('a');
        field.Type('b');

        Assert.True(field.Backspace());
        Assert.Equal("a", field.Text);

        field.Backspace();
        Assert.False(field.Backspace());
        Assert.True(field.IsEmpty);
    }

    [Fact]
    public void CharactersTheFieldRefuses_AreIgnored()
    {
        var field = new TextFieldModel(10, c => char.IsDigit(c));

        field.Type('1');
        field.Type('x');
        field.Type('2');

        Assert.Equal("12", field.Text);
    }

    [Fact]
    public void Handle_TreatsBackspaceAsDeleting_AndIgnoresOtherControlCharacters()
    {
        var field = new TextFieldModel(10, NewWorldRules.IsTypable);

        Assert.True(field.Handle('h'));
        Assert.True(field.Handle('i'));
        Assert.False(field.Handle('\r'));
        Assert.False(field.Handle('\t'));
        Assert.Equal("hi", field.Text);

        Assert.True(field.Handle('\b'));
        Assert.Equal("h", field.Text);
    }

    [Fact]
    public void SettingTheText_FollowsTheSameRules()
    {
        var field = new TextFieldModel(4, c => c != 'x');

        field.Text = "abxcdef";

        Assert.Equal("abcd", field.Text);

        field.Text = null;
        Assert.True(field.IsEmpty);
    }

    [Fact]
    public void Clear_EmptiesTheField()
    {
        var field = new TextFieldModel(5);
        field.Text = "abc";

        field.Clear();

        Assert.Equal(string.Empty, field.Text);
    }
}
