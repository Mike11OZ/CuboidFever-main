using System;
using System.Collections.Generic;
using System.Linq;

namespace CuboidFever;

// Where a command's answers go (the on-screen console, or a list in a test).
public interface ICommandSink
{
    void Reply(string text);
    void Error(string text);
}

// What a command is allowed to touch. Built once per world session.
public sealed class CommandContext
{
    public World World;
    public Player Player;
    public PlayerController Controller;
    public EntityManager Entities;
    public DayNightCycle DayNight;
    public ICommandSink Out;
}

// Thrown by a command to tell the player what they typed wrong. The message is shown as is.
public sealed class CommandException : Exception
{
    public CommandException(string message) : base(message) { }
}

public delegate void CommandAction(CommandContext context, string[] args);

public sealed class Command
{
    public string Name = "";
    public string Usage = "";          // for example: /spawn entity <id> [count] [x y z]
    public string Description = "";
    public CommandAction Run;

    // Optional: given the words already typed after the command name, returns what the next word could be (for Tab)
    public Func<CommandContext, string[], IEnumerable<string>> Complete;
}

public sealed class CommandRegistry
{
    private readonly Dictionary<string, Command> _commands = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<Command> All => _commands.Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase);

    public void Register(Command command) => _commands[command.Name] = command;

    public Command Find(string name) => _commands.TryGetValue(name, out var c) ? c : null;

    // Runs one line of input. Every outcome, good or bad, ends up as a Reply or Error on context.Out.
    public void Execute(string line, CommandContext context)
    {
        var tokens = CommandParser.Tokenize(line);
        if (tokens.Count == 0) return;

        var command = Find(tokens[0]);
        if (command == null)
        {
            context.Out.Error($"Unknown command '{tokens[0]}'. Type /help for the list.");
            return;
        }

        try
        {
            command.Run(context, tokens.Skip(1).ToArray());
        }
        catch (CommandException e)
        {
            context.Out.Error(e.Message);
        }
        catch (Exception e)
        {
            // A bug in a command must not take the game down
            Console.Error.WriteLine($"[Command] /{command.Name} failed: {e}");
            context.Out.Error($"/{command.Name} failed: {e.Message}");
        }
    }

    // What Tab can finish the last word of 'text' into. 'replaceFrom' is where that word starts in 'text'.
    public List<string> Complete(string text, CommandContext context, out int replaceFrom)
    {
        int start = text.Length;
        while (start > 0 && !char.IsWhiteSpace(text[start - 1])) start--;
        replaceFrom = start;

        string partial = text.Substring(start);
        var before = CommandParser.Tokenize(text.Substring(0, start));

        // First word: a command name (the slash is not part of the name)
        if (before.Count == 0)
        {
            if (partial.StartsWith('/')) { replaceFrom++; partial = partial.Substring(1); }

            return All.Select(c => c.Name)
                      .Where(n => n.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                      .ToList();
        }

        var command = Find(before[0]);
        if (command?.Complete == null) return new List<string>();

        return command.Complete(context, before.Skip(1).ToArray())
                      .Where(o => o.StartsWith(partial, StringComparison.OrdinalIgnoreCase))
                      .ToList();
    }
}
