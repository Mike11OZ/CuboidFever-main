using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CuboidFever;

// Turns what the player typed into words. No game types in here, so it can be tested.
public static class CommandParser
{
    // "/spawn entity scorpion 3" -> spawn, entity, scorpion, 3. One leading slash is optional and dropped.
    // Words are split on spaces; "double quotes" keep spaces inside one word.
    public static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        if (string.IsNullOrWhiteSpace(line)) return tokens;

        line = line.Trim();
        if (line[0] == '/') line = line.Substring(1);

        var word = new StringBuilder();
        bool inQuotes = false, haveWord = false;

        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                haveWord = true; // "" is an empty word
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (haveWord) tokens.Add(word.ToString());
                word.Clear();
                haveWord = false;
            }
            else
            {
                word.Append(c);
                haveWord = true;
            }
        }

        if (haveWord) tokens.Add(word.ToString());
        return tokens;
    }

    // A coordinate is a number (12.5), "~" (where the player is) or "~3" / "~-2" (that far from the player).
    // 'origin' is the player's value on that axis.
    public static bool TryParseCoordinate(string token, float origin, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(token)) return false;

        if (token[0] == '~')
        {
            if (token.Length == 1) { value = origin; return true; }

            if (!float.TryParse(token.AsSpan(1), NumberStyles.Float, CultureInfo.InvariantCulture, out float offset)) return false;
            value = origin + offset;
        }
        else if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        return float.IsFinite(value);
    }
}
