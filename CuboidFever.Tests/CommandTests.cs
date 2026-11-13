using System;
using System.Collections.Generic;
using System.Linq;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

public class CommandParserTests
{
    [Fact]
    public void SplitsWordsAndDropsOneLeadingSlash()
    {
        Assert.Equal(new[] { "spawn", "entity", "scorpion", "3" }, CommandParser.Tokenize("/spawn entity scorpion 3"));
        Assert.Equal(new[] { "tp", "1", "2", "3" }, CommandParser.Tokenize("  tp   1 2   3 "));
    }

    [Fact]
    public void QuotesKeepSpacesInOneWord()
    {
        Assert.Equal(new[] { "say", "hello world" }, CommandParser.Tokenize("say \"hello world\""));
    }

    [Fact]
    public void EmptyInputGivesNoWords()
    {
        Assert.Empty(CommandParser.Tokenize(""));
        Assert.Empty(CommandParser.Tokenize("   "));
        Assert.Empty(CommandParser.Tokenize("/"));
    }

    [Theory]
    [InlineData("12.5", 100f, 12.5f)]
    [InlineData("-3", 100f, -3f)]
    [InlineData("~", 100f, 100f)]
    [InlineData("~5", 100f, 105f)]
    [InlineData("~-2.5", 100f, 97.5f)]
    public void ReadsCoordinates(string token, float origin, float expected)
    {
        Assert.True(CommandParser.TryParseCoordinate(token, origin, out float value));
        Assert.Equal(expected, value, 3);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("~x")]
    [InlineData("NaN")]
    public void RejectsBadCoordinates(string token)
    {
        Assert.False(CommandParser.TryParseCoordinate(token, 0f, out _));
    }
}

public class CommandRegistryTests
{
    private sealed class Sink : ICommandSink
    {
        public readonly List<string> Replies = new();
        public readonly List<string> Errors = new();
        public void Reply(string text) => Replies.Add(text);
        public void Error(string text) => Errors.Add(text);
    }

    private static (CommandRegistry registry, CommandContext context, Sink sink) Make()
    {
        var sink = new Sink();
        var registry = new CommandRegistry();

        registry.Register(new Command
        {
            Name = "echo",
            Run = (c, a) => c.Out.Reply(string.Join(" ", a)),
            Complete = (c, a) => a.Length == 0 ? new[] { "alpha", "beta", "alps" } : Array.Empty<string>()
        });
        registry.Register(new Command { Name = "fail", Run = (c, a) => throw new CommandException("nope") });
        registry.Register(new Command { Name = "crash", Run = (c, a) => throw new InvalidOperationException("bug") });

        return (registry, new CommandContext { Out = sink }, sink);
    }

    [Fact]
    public void RunsACommandWithItsArguments()
    {
        var (registry, context, sink) = Make();
        registry.Execute("/ECHO one two", context); // names are not case sensitive

        Assert.Equal(new[] { "one two" }, sink.Replies);
        Assert.Empty(sink.Errors);
    }

    [Fact]
    public void UnknownCommandIsReported()
    {
        var (registry, context, sink) = Make();
        registry.Execute("/dance", context);

        Assert.Single(sink.Errors);
        Assert.Contains("dance", sink.Errors[0]);
    }

    [Fact]
    public void CommandExceptionMessageIsShownAsIs()
    {
        var (registry, context, sink) = Make();
        registry.Execute("fail", context);

        Assert.Equal(new[] { "nope" }, sink.Errors);
    }

    [Fact]
    public void ABugInACommandDoesNotEscape()
    {
        var (registry, context, sink) = Make();
        registry.Execute("crash", context); // must not throw

        Assert.Single(sink.Errors);
    }

    [Fact]
    public void BlankInputDoesNothing()
    {
        var (registry, context, sink) = Make();
        registry.Execute("   ", context);

        Assert.Empty(sink.Replies);
        Assert.Empty(sink.Errors);
    }

    [Fact]
    public void CompletesCommandNamesAfterASlash()
    {
        var (registry, context, _) = Make();

        var options = registry.Complete("/e", context, out int from);

        Assert.Equal(new[] { "echo" }, options);
        Assert.Equal(1, from); // the slash is left alone
    }

    [Fact]
    public void CompletesArgumentsFromTheCommand()
    {
        var (registry, context, _) = Make();

        var options = registry.Complete("/echo al", context, out int from);

        Assert.Equal(new[] { "alpha", "alps" }, options.OrderBy(o => o));
        Assert.Equal(6, from);
    }

    [Fact]
    public void AfterASpaceTheNextWordIsCompleted()
    {
        var (registry, context, _) = Make();

        var options = registry.Complete("/echo ", context, out int from);

        Assert.Equal(3, options.Count);
        Assert.Equal(6, from);
    }
}
