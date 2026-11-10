using System.Collections.Generic;
using System.IO;
using System.Linq;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

public class BlockStatesTests
{
    private static BlockDefinition Door() => new()
    {
        Name = "test:door",
        Textures = new() { ["all"] = "test:plain" },
        Properties = new()
        {
            ["facing"] = new() { "north", "east", "south", "west" },
            ["half"] = new() { "lower", "upper" },
            ["open"] = new() { "false", "true" },
        },
        Variants = new()
        {
            new() { When = new() { ["half"] = "lower" }, Textures = new() { ["all"] = "test:bottom", ["edge"] = "test:bottom_s" } },
            new() { When = new() { ["half"] = "upper" }, Textures = new() { ["all"] = "test:top" } },
        },
    };

    [Fact]
    public void BlockWithoutProperties_IsReturnedUnchanged()
    {
        var stone = new BlockDefinition { Name = "test:stone" };

        var states = BlockStates.Expand(stone);

        Assert.Single(states);
        Assert.Same(stone, states[0]);
        Assert.Null(states[0].BaseName);
    }

    [Fact]
    public void EveryCombinationBecomesABlock()
    {
        var states = BlockStates.Expand(Door());

        Assert.Equal(16, states.Count);
        Assert.Equal(16, states.Select(s => s.Name).Distinct().Count());
        Assert.All(states, s => Assert.Equal("test:door", s.BaseName));
    }

    [Fact]
    public void FirstStateIsTheDefault()
    {
        var first = BlockStates.Expand(Door())[0];

        Assert.Equal("test:door[facing=north,half=lower,open=false]", first.Name);
    }

    [Fact]
    public void NamesListPropertiesInSortedOrder()
    {
        var state = new Dictionary<string, string> { ["open"] = "true", ["facing"] = "east", ["half"] = "upper" };

        Assert.Equal("test:door[facing=east,half=upper,open=true]", BlockStates.StateName("test:door", state));
        Assert.Equal("test:stone", BlockStates.StateName("test:stone", new Dictionary<string, string>()));
    }

    [Fact]
    public void VariantsOverrideTexturesOnlyForMatchingStates()
    {
        var states = BlockStates.Expand(Door());

        var lower = states.First(s => s.State["half"] == "lower");
        var upper = states.First(s => s.State["half"] == "upper");

        Assert.Equal("test:bottom", lower.Textures["all"]);
        Assert.Equal("test:bottom_s", lower.Textures["edge"]);
        Assert.Equal("test:top", upper.Textures["all"]);
        Assert.False(upper.Textures.ContainsKey("edge"));
    }

    [Fact]
    public void ExpandingDoesNotChangeTheOriginal()
    {
        var door = Door();

        BlockStates.Expand(door);

        Assert.Equal("test:plain", door.Textures["all"]);
        Assert.Equal(3, door.Properties.Count);
    }

    [Fact]
    public void VariantForAnUnknownStateIsRejected()
    {
        var door = Door();
        door.Variants.Add(new() { When = new() { ["half"] = "middle" } });

        Assert.Throws<InvalidDataException>(() => BlockStates.Expand(door));
    }

    [Fact]
    public void VariantsWithoutPropertiesAreRejected()
    {
        var block = new BlockDefinition { Name = "test:x", Variants = new() { new() } };

        Assert.Throws<InvalidDataException>(() => BlockStates.Expand(block));
    }

    [Fact]
    public void BadPropertiesAreRejected()
    {
        var noValues = new BlockDefinition { Name = "a", Properties = new() { ["p"] = new() } };
        var repeated = new BlockDefinition { Name = "b", Properties = new() { ["p"] = new() { "x", "x" } } };
        var badName = new BlockDefinition { Name = "c", Properties = new() { ["p=q"] = new() { "x" } } };
        var badValue = new BlockDefinition { Name = "d", Properties = new() { ["p"] = new() { "x,y" } } };

        Assert.Throws<InvalidDataException>(() => BlockStates.Expand(noValues));
        Assert.Throws<InvalidDataException>(() => BlockStates.Expand(repeated));
        Assert.Throws<InvalidDataException>(() => BlockStates.Expand(badName));
        Assert.Throws<InvalidDataException>(() => BlockStates.Expand(badValue));
    }

    [Fact]
    public void TooManyStatesAreRejected()
    {
        var many = new BlockDefinition
        {
            Name = "test:many",
            Properties = new()
            {
                ["a"] = Enumerable.Range(0, 9).Select(i => i.ToString()).ToList(),
                ["b"] = Enumerable.Range(0, 9).Select(i => i.ToString()).ToList(),
            },
        };

        Assert.Throws<InvalidDataException>(() => BlockStates.Expand(many));
    }
}
