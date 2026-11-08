using System;
using System.Globalization;

namespace CuboidFever;

// What the New World screen accepts and how it turns the typed text into a world: the name and the seed.
public static class NewWorldRules
{
    public const int MaxNameLength = 32;
    public const int MaxSeedLength = 24;

    // The menu font covers the plain keyboard characters (space to ~), so that is what can be typed. Anything
    // else a keyboard sends (accents, emoji, control characters) is ignored instead of showing up as '?'.
    public static bool IsTypable(char c) => c >= ' ' && c <= '~';

    // The name to save: surrounding spaces removed, and the fallback ("World 3") when nothing was typed.
    public static string CleanName(string typed, string fallback)
    {
        string name = (typed ?? string.Empty).Trim();
        return name.Length == 0 ? fallback : name;
    }

    // The seed for the typed text, like Minecraft's seed box:
    //   blank           -> a random seed (from the function given, so tests can fix it)
    //   a whole number  -> that number
    //   anything else   -> a number made from the text, always the same for the same text
    public static int ParseSeed(string typed, Func<int> random)
    {
        string text = (typed ?? string.Empty).Trim();

        if (text.Length == 0)
            return random();

        if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number))
            return number;

        return StableHash(text);
    }

    // FNV-1a over the characters. string.GetHashCode is different on every run of the program, which would
    // give the same seed text a different world each time, so it cannot be used here.
    public static int StableHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)hash;
        }
    }
}
