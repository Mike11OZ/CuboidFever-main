using System;
using System.IO;

namespace CuboidFever;

// Deleting a world is permanent and recursive, so it is only allowed on something that is clearly a world:
// a folder directly inside the saves folder that has a level.json. Anything else (the saves folder itself, a
// folder somewhere else, a world's inner "columns" folder, a shortcut/junction pointing elsewhere) is refused,
// so a wrong path can never wipe out files that are not a world.
public static class WorldDeletion
{
    public static bool IsWorldFolder(string savesRoot, string folder)
    {
        if (string.IsNullOrWhiteSpace(savesRoot) || string.IsNullOrWhiteSpace(folder)) return false;

        try
        {
            string root = TrimSeparators(Path.GetFullPath(savesRoot));
            string full = TrimSeparators(Path.GetFullPath(folder));

            string parent = Path.GetDirectoryName(full);
            if (parent == null) return false;

            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(TrimSeparators(parent), root, comparison)) return false;

            if (!Directory.Exists(full)) return false;
            if ((new DirectoryInfo(full).Attributes & FileAttributes.ReparsePoint) != 0) return false;

            return File.Exists(WorldInfo.PathIn(full));
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
        {
            return false;
        }
    }

    // Deletes the world folder and everything in it. Throws InvalidOperationException if the folder is not a
    // world (nothing is touched), or IOException / UnauthorizedAccessException if the files cannot be removed
    // (for example, another program has one open); the caller shows that to the player.
    public static void Delete(string savesRoot, string folder)
    {
        if (!IsWorldFolder(savesRoot, folder))
            throw new InvalidOperationException($"'{folder}' is not a saved world, so it was not deleted.");

        Directory.Delete(Path.GetFullPath(folder), recursive: true);
    }

    private static string TrimSeparators(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
