using System;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

public class NewWorldRulesTests
{
    [Fact]
    public void BlankSeed_UsesTheRandomSource()
    {
        Assert.Equal(1234, NewWorldRules.ParseSeed("", () => 1234));
        Assert.Equal(1234, NewWorldRules.ParseSeed("   ", () => 1234));
        Assert.Equal(1234, NewWorldRules.ParseSeed(null, () => 1234));
    }

    [Fact]
    public void NumberSeed_IsUsedAsIs()
    {
        Assert.Equal(706944, NewWorldRules.ParseSeed("706944", () => 1));
        Assert.Equal(-5, NewWorldRules.ParseSeed(" -5 ", () => 1));
        Assert.Equal(0, NewWorldRules.ParseSeed("0", () => 1));
    }

    [Fact]
    public void TextSeed_GivesTheSameNumberEveryTime()
    {
        int first = NewWorldRules.ParseSeed("hello world", () => 1);
        int second = NewWorldRules.ParseSeed("hello world", () => 2);

        Assert.Equal(first, second);
        Assert.False(first == NewWorldRules.ParseSeed("hello worle", () => 1));
    }

    [Fact]
    public void TooBigNumber_IsTreatedAsText_NotAnError()
    {
        int seed = NewWorldRules.ParseSeed("99999999999999999999", () => 1);

        Assert.Equal(NewWorldRules.StableHash("99999999999999999999"), seed);
    }

    [Fact]
    public void StableHash_HasAKnownValue()
    {
        // FNV-1a of "a" is 0xE40C292C: guards against the hash changing between versions, which would change
        // every world made from a text seed
        Assert.Equal(unchecked((int)0xE40C292C), NewWorldRules.StableHash("a"));
    }

    [Fact]
    public void CleanName_TrimsAndFallsBack()
    {
        Assert.Equal("My World", NewWorldRules.CleanName("  My World ", "World 1"));
        Assert.Equal("World 1", NewWorldRules.CleanName("   ", "World 1"));
        Assert.Equal("World 1", NewWorldRules.CleanName(null, "World 1"));
    }

    [Fact]
    public void OnlyPlainKeyboardCharacters_AreTypable()
    {
        Assert.True(NewWorldRules.IsTypable('a'));
        Assert.True(NewWorldRules.IsTypable(' '));
        Assert.True(NewWorldRules.IsTypable('~'));
        Assert.False(NewWorldRules.IsTypable('\n'));
        Assert.False(NewWorldRules.IsTypable('\t'));
        Assert.False(NewWorldRules.IsTypable('é'));
        Assert.False(NewWorldRules.IsTypable('\b'));
    }
}
