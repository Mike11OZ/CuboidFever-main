using System;
using System.Collections.Generic;

namespace CuboidFever;

// Breaks a message into lines that fit a width. The width of text is asked of a function, so this has no
// dependence on a font and can be tested (the game passes SpriteFont.MeasureString).
public static class TextWrap
{
    // Words are kept whole where possible; a single word wider than the width is cut into pieces. At most
    // maxLines lines are returned, and if the text did not all fit the last line ends with "...".
    public static List<string> Wrap(string text, Func<string, float> measure, float maxWidth, int maxLines)
    {
        var lines = new List<string>();
        if (string.IsNullOrWhiteSpace(text) || maxLines <= 0) return lines;

        var current = string.Empty;
        bool truncated = false;

        foreach (string word in text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = current.Length == 0 ? word : current + " " + word;

            if (measure(candidate) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
            {
                lines.Add(current);
                current = string.Empty;
            }

            // The word alone may still be too wide: cut it into pieces that fit
            string rest = word;
            while (measure(rest) > maxWidth && rest.Length > 1)
            {
                int take = rest.Length - 1;
                while (take > 1 && measure(rest.Substring(0, take)) > maxWidth) take--;

                lines.Add(rest.Substring(0, take));
                rest = rest.Substring(take);
            }
            current = rest;
        }

        if (current.Length > 0) lines.Add(current);

        if (lines.Count > maxLines)
        {
            lines.RemoveRange(maxLines, lines.Count - maxLines);
            truncated = true;
        }

        if (truncated)
        {
            string last = lines[maxLines - 1];
            while (last.Length > 0 && measure(last + "...") > maxWidth) last = last.Substring(0, last.Length - 1);
            lines[maxLines - 1] = last.TrimEnd() + "...";
        }

        return lines;
    }
}
