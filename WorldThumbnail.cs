using System;
using System.IO;

namespace CuboidFever;

// The picture shown next to a world in the world list: <world folder>/thumbnail.png.
//
// This only moves PNG bytes to and from disk. Taking the picture (Game1) and drawing it (the world list) need
// the graphics device, so they live elsewhere. A missing, empty or unreadable thumbnail is never an error: the
// list just shows a placeholder, and the next time the player leaves the world a new one is written.
public static class WorldThumbnail
{
    public const string FileName = "thumbnail.png";

    // Thumbnails are small; anything bigger than this is not one of ours and is ignored.
    public const int MaxBytes = 4 * 1024 * 1024;

    public static string PathIn(string worldFolder) => Path.Combine(worldFolder, FileName);

    // Replaces the thumbnail. Written atomically, so a crash mid-write leaves the old picture, not half a PNG.
    public static void Write(string worldFolder, byte[] png)
    {
        if (png == null || png.Length == 0) return;
        AtomicFile.WriteAllBytes(PathIn(worldFolder), png);
    }

    // The PNG bytes, or null when there is no usable thumbnail.
    public static byte[] TryRead(string worldFolder)
    {
        try
        {
            string path = PathIn(worldFolder);
            if (!File.Exists(path)) return null;

            var file = new FileInfo(path);
            if (file.Length == 0 || file.Length > MaxBytes) return null;

            return File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            return null;
        }
    }
}
