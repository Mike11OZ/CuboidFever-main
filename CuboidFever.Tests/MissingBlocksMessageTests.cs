using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

public class MissingBlocksMessageTests
{
    [Fact]
    public void NothingMissing_GivesNoMessage()
    {
        Assert.Null(MissingBlocksMessage.Make(new string[0]));
        Assert.Null(MissingBlocksMessage.Make(null));
    }

    [Fact]
    public void FewBlocks_AreAllNamed()
    {
        string message = MissingBlocksMessage.Make(new[] { "a:stone", "a:dirt" });

        Assert.Contains("a:stone, a:dirt", message);
        Assert.False(message.Contains("more"));
    }

    [Fact]
    public void ManyBlocks_AreCounted()
    {
        string message = MissingBlocksMessage.Make(new[] { "a", "b", "c", "d", "e" });

        Assert.Contains("a, b, c and 2 more", message);
    }
}
