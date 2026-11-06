using System;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// How big the user interface should be drawn for the current window size, as a whole number: 1, 2, 3...
//
// Menu sizes (button height, gaps, text) are written as "how big it is in an 800x480 window", the default
// windowed size. Multiplying them by this scale gives the same layout, in the same proportions, on a bigger
// screen. Without it, buttons sized as a share of the screen grow in fullscreen while the text on them
// stays the same size.
//
// The scale is a whole number on purpose. The menus use a pixel font drawn with point sampling, and at a
// fractional scale some of the font's pixels would come out wider than others. Whole numbers keep every
// pixel the same size, the way Minecraft's GUI scale does.
//
// The largest scale that fits the window is "Auto". The player can choose a smaller one in the settings
// menu, but not a larger one: the menus are laid out to just fit at the largest scale, so anything bigger
// would run off the screen.
//
//     800x480 -> 1     1920x1080 -> 2     2560x1440 -> 3     3840x2160 -> 4
//
// Use it for anything with a size in pixels:
//     int scale = GuiScale.For(GraphicsDevice);
//     int buttonHeight = 48 * scale;
//     sprites.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
public static class GuiScale
{
    // The window size the menus are designed for. At this size or anything smaller the scale is 1.
    public const int ReferenceWidth = 800;
    public const int ReferenceHeight = 480;

    // The player's choice: 0 means Auto, otherwise the largest scale they will accept.
    // It is a limit rather than an exact value, so moving to a smaller window (fullscreen to windowed)
    // can never leave the menus too big to fit.
    public static int Setting { get; set; } = 0;

    public static int For(GraphicsDevice graphics) =>
        For(graphics.Viewport.Width, graphics.Viewport.Height);

    // The scale to draw at: the largest that fits, or the player's limit if that is smaller.
    public static int For(int viewportWidth, int viewportHeight)
    {
        int fit = MaxFit(viewportWidth, viewportHeight);
        return Setting <= 0 ? fit : Math.Min(Setting, fit);
    }

    // The largest whole number of reference windows that fits in the given size both ways.
    // Never less than 1, so a window smaller than the reference still gets a usable interface.
    public static int MaxFit(int viewportWidth, int viewportHeight) =>
        Math.Max(1, Math.Min(viewportWidth / ReferenceWidth, viewportHeight / ReferenceHeight));

    // Moves to the next choice for the settings button: Auto, then 1, 2, ... up to one below the largest
    // that fits, then back to Auto. The largest that fits is left out because Auto already means it.
    // In a window where only 1 fits there is nothing to choose and this does nothing.
    public static void CycleSetting(GraphicsDevice graphics)
    {
        int fit = MaxFit(graphics.Viewport.Width, graphics.Viewport.Height);

        int current = Setting <= 0 ? 0 : Math.Min(Setting, fit);
        int next = current + 1;

        Setting = next >= fit ? 0 : next;
    }

    // Text for the settings button: "Auto (2)" shows what Auto currently gives, "1" is a chosen limit.
    public static string Label(GraphicsDevice graphics)
    {
        int fit = MaxFit(graphics.Viewport.Width, graphics.Viewport.Height);

        return Setting <= 0 ? $"Auto ({fit})" : Math.Min(Setting, fit).ToString();
    }
}
