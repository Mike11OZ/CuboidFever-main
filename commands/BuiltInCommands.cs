using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// The commands the game ships with. To add one, write a method like the ones below and add a Register line.
public static class BuiltInCommands
{
    private const int MaxSpawnCount = 50;
    private const float SpawnSpread = 2f;       // blocks: how far apart a group of spawned entities lands
    private const float SpawnAheadDistance = 3f; // blocks in front of the player when no position is given

    private const string SpawnUsage = "/spawn entity <id> [count] [x y z]";

    public static void Register(CommandRegistry registry)
    {
        registry.Register(new Command
        {
            Name = "help",
            Usage = "/help [command]",
            Description = "Lists the commands, or explains one.",
            Run = (c, a) => Help(registry, c, a),
            Complete = (c, a) => a.Length == 0 ? registry.All.Select(x => x.Name) : Array.Empty<string>()
        });

        registry.Register(new Command
        {
            Name = "spawn",
            Usage = SpawnUsage,
            Description = "Spawns creatures. With no position they appear 3 blocks in front of you, on the ground. " +
                          "x y z may use ~ for your own position (~ ~5 ~ is 5 blocks above you).",
            Run = Spawn,
            Complete = (c, a) => a.Length switch
            {
                0 => new[] { "entity" },
                1 => EntityTypes.Ids,
                _ => Array.Empty<string>()
            }
        });

        registry.Register(new Command
        {
            Name = "kill",
            Usage = "/kill [id|all]",
            Description = "Removes every entity of that kind, or all of them (the default).",
            Run = Kill,
            Complete = (c, a) => a.Length == 0 ? EntityTypes.Ids.Append("all") : Array.Empty<string>()
        });

        registry.Register(new Command
        {
            Name = "entities",
            Usage = "/entities",
            Description = "Counts entities by kind and shows what the five nearest are doing.",
            Run = ListEntities
        });

        registry.Register(new Command
        {
            Name = "tp",
            Usage = "/tp <x> <y> <z>",
            Description = "Teleports you. ~ means your own position (~ ~10 ~ is 10 blocks up).",
            Run = Teleport
        });

        registry.Register(new Command
        {
            Name = "time",
            Usage = "/time query | /time set <sunrise|day|noon|sunset|night|midnight|HH:MM>",
            Description = "Shows or changes the time of day.",
            Run = Time,
            Complete = (c, a) => a.Length switch
            {
                0 => new[] { "query", "set" },
                1 when a[0].Equals("set", StringComparison.OrdinalIgnoreCase) => TimePresets.Keys,
                _ => Array.Empty<string>()
            }
        });
    }

    // =====================  /help  =====================

    private static void Help(CommandRegistry registry, CommandContext c, string[] a)
    {
        if (a.Length == 0)
        {
            c.Out.Reply("Commands: " + string.Join(", ", registry.All.Select(x => "/" + x.Name)));
            c.Out.Reply("Type /help <command> for details. Tab completes names.");
            return;
        }

        var command = registry.Find(a[0].TrimStart('/'));
        if (command == null) throw new CommandException($"There is no command '{a[0]}'.");

        c.Out.Reply(command.Usage);
        c.Out.Reply(command.Description);
    }

    // =====================  /spawn  =====================

    private static void Spawn(CommandContext c, string[] a)
    {
        if (a.Length < 2 || !a[0].Equals("entity", StringComparison.OrdinalIgnoreCase))
            throw new CommandException("Usage: " + SpawnUsage);

        var type = EntityTypes.Get(a[1])
            ?? throw new CommandException($"There is no entity '{a[1]}'. Known: {string.Join(", ", EntityTypes.Ids)}.");

        // After "entity <id>" comes nothing, a count, three coordinates, or both
        int count = 1;
        int coordStart = -1;
        switch (a.Length - 2)
        {
            case 0: break;
            case 1: count = ParseCount(a[2]); break;
            case 3: coordStart = 2; break;
            case 4: count = ParseCount(a[2]); coordStart = 3; break;
            default: throw new CommandException("Usage: " + SpawnUsage);
        }

        Vector3 feet = c.Player.Position;
        float x, y, z;
        bool findGround;

        if (coordStart >= 0)
        {
            x = Coordinate(a[coordStart], feet.X);
            y = Coordinate(a[coordStart + 1], feet.Y);
            z = Coordinate(a[coordStart + 2], feet.Z);
            findGround = false; // the player said exactly where
        }
        else
        {
            Vector3 ahead = c.Controller.Forward;
            ahead.Y = 0f;
            if (ahead != Vector3.Zero) ahead.Normalize();

            x = feet.X + ahead.X * SpawnAheadDistance;
            z = feet.Z + ahead.Z * SpawnAheadDistance;
            y = feet.Y;
            findGround = true;
        }

        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            float px = x, pz = z;
            if (count > 1)
            {
                px += (Random.Shared.NextSingle() * 2f - 1f) * SpawnSpread;
                pz += (Random.Shared.NextSingle() * 2f - 1f) * SpawnSpread;
            }

            int bx = (int)MathF.Floor(px), bz = (int)MathF.Floor(pz);
            if (!c.World.IsColumnLoaded(bx, bz)) continue; // nothing to stand on there yet

            float py = findGround ? GroundY(c.World, bx, (int)MathF.Floor(y), bz) : y;
            float yaw = Random.Shared.NextSingle() * MathHelper.TwoPi;

            c.Entities.Spawn(type.Create(new Vector3(px, py, pz), yaw));
            spawned++;
        }

        if (spawned == 0) throw new CommandException("That spot has not loaded yet, so nothing was spawned.");

        c.Out.Reply(spawned == count
            ? $"Spawned {spawned} {type.Id}."
            : $"Spawned {spawned} of {count} {type.Id} (the rest landed in unloaded terrain).");
    }

    private static int ParseCount(string text)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n < 1 || n > MaxSpawnCount)
            throw new CommandException($"The count must be a whole number from 1 to {MaxSpawnCount}.");
        return n;
    }

    // Starts two blocks above 'startY' and comes down to the first solid block, so entities land on the ground
    // even when it is a step higher or lower than the player's feet.
    private static float GroundY(World world, int x, int startY, int z)
    {
        int y = Math.Min(startY + 2, World.Height - 1);
        while (y > 0 && !world.IsSolid(x, y - 1, z)) y--;
        return y;
    }

    // =====================  /kill  =====================

    private static void Kill(CommandContext c, string[] a)
    {
        string which = a.Length > 0 ? a[0] : "all";
        if (a.Length > 1) throw new CommandException("Usage: /kill [id|all]");

        bool all = which.Equals("all", StringComparison.OrdinalIgnoreCase);
        if (!all && EntityTypes.Get(which) == null)
            throw new CommandException($"There is no entity '{which}'. Known: {string.Join(", ", EntityTypes.Ids)}, all.");

        int removed = 0;
        foreach (var e in c.Entities.All)
        {
            if (e.Removed || !(all || e.Type.Id.Equals(which, StringComparison.OrdinalIgnoreCase))) continue;
            e.Removed = true; // the manager drops it at the end of its next update
            removed++;
        }

        c.Out.Reply(removed == 0 ? "Nothing to remove." : $"Removed {removed} {(removed == 1 ? "entity" : "entities")}.");
    }

    // =====================  /entities  =====================

    private static void ListEntities(CommandContext c, string[] a)
    {
        if (c.Entities.Count == 0)
        {
            c.Out.Reply("There are no entities.");
            return;
        }

        foreach (var group in c.Entities.All.GroupBy(e => e.Type.Id))
            c.Out.Reply($"{group.Key}: {group.Count()}");

        Vector3 here = c.Player.Position;
        foreach (var mob in c.Entities.All.OfType<MobEntity>().OrderBy(m => Vector3.DistanceSquared(m.Position, here)).Take(5))
            c.Out.Reply($"  {mob.Type.Id}, {Vector3.Distance(mob.Position, here):F1} blocks away: {mob.Goals.CurrentName}");
    }

    // =====================  /tp  =====================

    private static void Teleport(CommandContext c, string[] a)
    {
        if (a.Length != 3) throw new CommandException("Usage: /tp <x> <y> <z>");

        Vector3 here = c.Player.Position;
        var target = new Vector3(Coordinate(a[0], here.X), Coordinate(a[1], here.Y), Coordinate(a[2], here.Z));
        target.Y = MathHelper.Clamp(target.Y, 0f, World.Height);

        c.Player.Position = target;
        c.Player.Velocity = Vector3.Zero; // otherwise a fall in progress would carry on at its old speed

        c.Out.Reply($"Teleported to {target.X:F1} {target.Y:F1} {target.Z:F1}.");
    }

    private static float Coordinate(string token, float origin)
    {
        if (!CommandParser.TryParseCoordinate(token, origin, out float value))
            throw new CommandException($"'{token}' is not a coordinate. Use a number, ~ or ~5.");
        return value;
    }

    // =====================  /time  =====================

    // DayNightCycle.TimeOfDay: 0 = sunrise (06:00), 0.25 = noon, 0.5 = sunset, 0.75 = midnight
    private static readonly Dictionary<string, float> TimePresets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sunrise"] = 0f,
        ["day"] = 0.04f,      // about 07:00, where a new world starts
        ["noon"] = 0.25f,
        ["sunset"] = 0.5f,
        ["night"] = 0.6f,
        ["midnight"] = 0.75f,
    };

    private static void Time(CommandContext c, string[] a)
    {
        if (a.Length == 1 && a[0].Equals("query", StringComparison.OrdinalIgnoreCase))
        {
            c.Out.Reply($"It is {c.DayNight.TimeString}{(c.DayNight.Paused ? " (time is paused)" : "")}.");
            return;
        }

        if (a.Length != 2 || !a[0].Equals("set", StringComparison.OrdinalIgnoreCase))
            throw new CommandException("Usage: /time query | /time set <sunrise|day|noon|sunset|night|midnight|HH:MM>");

        float timeOfDay;
        if (!TimePresets.TryGetValue(a[1], out timeOfDay) && !TryParseClock(a[1], out timeOfDay))
            throw new CommandException($"'{a[1]}' is not a time. Use a name ({string.Join(", ", TimePresets.Keys)}) or HH:MM like 18:30.");

        c.DayNight.TimeOfDay = timeOfDay;
        c.DayNight.Scrub(0f); // brings the sky colours in line with the new time
        c.Out.Reply($"The time is now {c.DayNight.TimeString}.");
    }

    // "18:30" -> TimeOfDay. The clock starts at 06:00 = 0.
    private static bool TryParseClock(string text, out float timeOfDay)
    {
        timeOfDay = 0f;
        var parts = text.Split(':');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int h) || h > 23) return false;
        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int m) || m > 59) return false;

        float hours = h + m / 60f;
        timeOfDay = ((hours - 6f) % 24f + 24f) % 24f / 24f;
        return true;
    }
}
