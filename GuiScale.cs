using System;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// Shared GUI sizing. Layout is designed around an 800x480 reference window.
// The scale remains fractional until a value is converted into screen pixels.
public static class GuiScale
{
    public const int ReferenceWidth = 800;
    public const int ReferenceHeight = 480;

    // A little smaller than the original font size at every GUI scale.
    public const float FontScale = 0.75f;

    // 0 means Auto; otherwise this is the user's maximum preferred scale.
    public static float Setting { get; set; } = 1.5f;

    public static float For(GraphicsDevice graphics) =>
        For(graphics.Viewport.Width, graphics.Viewport.Height);

    // Auto chooses the largest half-step that fits the reference layout.
    public static float For(int viewportWidth, int viewportHeight)
    {
        float fit = MaxFit(viewportWidth, viewportHeight);
        return Setting <= 0f ? fit : Math.Min(Setting, fit);
    }

    public static float MaxFit(int viewportWidth, int viewportHeight)
    {
        float fit = Math.Min(
            viewportWidth / (float)ReferenceWidth,
            viewportHeight / (float)ReferenceHeight);

        // GUI choices use half-step increments. Keep at least 1x for small windows.
        return Math.Max(1f, MathF.Floor(fit * 2f + 0.0001f) / 2f);
    }

    // Convert a design-space measurement to an integer screen-pixel measurement.
    public static int Pixels(float value) => (int)MathF.Round(value);

    public static float Text(float guiScale) => guiScale * FontScale;

    // Choices intentionally include 1.5x but otherwise retain the original whole-number steps.
    private static readonly float[] Choices = { 1f, 1.5f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f };

    public static void CycleSetting(GraphicsDevice graphics)
    {
        float fit = MaxFit(graphics.Viewport.Width, graphics.Viewport.Height);
        float current = Setting <= 0f ? 0f : Math.Min(Setting, fit);

        foreach (float choice in Choices)
        {
            // Auto already represents the largest scale that fits, so it is not offered twice.
            if (choice > current && choice < fit)
            {
                Setting = choice;
                return;
            }
        }

        Setting = 0f;
    }

    public static string Label(GraphicsDevice graphics)
    {
        float fit = MaxFit(graphics.Viewport.Width, graphics.Viewport.Height);
        return Setting <= 0f ? $"Auto ({Format(fit)})" : Format(Math.Min(Setting, fit));
    }

    private static string Format(float value) =>
        value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
}
