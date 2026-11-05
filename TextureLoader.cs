using System;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

public static class TextureLoader
{
    // Loads a texture relative to the game's assets directory.
    // Returns null if the file cannot be loaded.
    public static Texture2D Load(GraphicsDevice graphics, string relativePath)
    {
        string fullPath = Path.Combine(
            AppContext.BaseDirectory,
            "assets",
            relativePath
        );

        return LoadFromFile(graphics, fullPath);
    }

    // Loads a texture from an absolute filesystem path.
    // Returns null if the file cannot be loaded.
    public static Texture2D LoadFromFile(GraphicsDevice graphics, string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"[Warning] Texture not found: {path}");
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            return Texture2D.FromStream(graphics, stream);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] Failed to load texture: {path} ({ex.Message})");
            return null;
        }
    }
}