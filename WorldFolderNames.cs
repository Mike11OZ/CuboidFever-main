using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CuboidFever;

// Turns a world's display name into a folder name that works on every operating system and is not already taken.
public static class WorldFolderNames
{
    private const int MaxLength = 60;

    // Windows refuses these as file or folder names, with or without an extension
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    // 'exists' says whether a folder name is already in use; if it is, " (2)", " (3)" ... is added until one is free.
    public static string Make(string name, Func<string, bool> exists)
    {
        char[] invalid = Path.GetInvalidFileNameChars();

        var builder = new StringBuilder();
        foreach (char c in name ?? string.Empty)
            builder.Append(char.IsControl(c) || Array.IndexOf(invalid, c) >= 0 ? '_' : c);

        // Windows also drops trailing dots and spaces, which would make two different names collide
        string baseName = builder.ToString().Trim().TrimEnd('.', ' ');
        if (baseName.Length > MaxLength)
            baseName = baseName.Substring(0, MaxLength).TrimEnd('.', ' ');
        if (baseName.Length == 0)
            baseName = "World";

        if (Reserved.Contains(baseName.Split('.')[0]))
            baseName += "_";

        string candidate = baseName;
        for (int n = 2; exists(candidate); n++)
            candidate = $"{baseName} ({n})";

        return candidate;
    }
}
