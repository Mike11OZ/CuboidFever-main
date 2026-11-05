using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// The shared look of every loading screen: the logo centered on a dark background, with an optional
// progress bar underneath. It holds no loading logic and no per-use state. Whatever is doing the
// loading (StartupScreen, SpawnSequence, ...) decides when to show it and what progress to pass in.
//
// Create one, call LoadContent once, and hand the same instance to everything that needs a loading screen.
public class LoadingScreen
{
    private Texture2D _logo;
    private Texture2D _pixel;

    // Hex #14121d converted to RGB (Red: 20, Green: 18, Blue: 29). Public so the menus can share the
    // background and switching between screens doesn't flash.
    public static readonly Color BackgroundColor = new Color(20, 18, 29);

    // Progress bar colors: a track slightly lighter than the background, and a near-white fill
    private readonly Color _barTrackColor = new Color(44, 40, 62);
    private readonly Color _barFillColor = new Color(235, 235, 245);

    // The logo is scaled to this fraction of the screen height and centered, as it always has been.
    private const float LogoHeightFraction = 0.5f;

    // Progress bar layout, as fractions of the screen so it scales with the window
    private const float BarWidthFraction = 0.3f;
    private const float BarHeightFraction = 0.012f;
    private const int BarMinHeight = 6;

    // Gap between the bottom of the logo and the top of the bar
    private const float BarGapFraction = 0.06f;

    public void LoadContent(GraphicsDevice graphics)
    {
        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });

        string logoPath = Path.Combine(AppContext.BaseDirectory, "assets", "oz.png");

        if (File.Exists(logoPath))
        {
            using var stream = File.OpenRead(logoPath);
            _logo = Texture2D.FromStream(graphics, stream);
        }
        else
        {
            Console.WriteLine($"[Warning] Loading screen logo not found at: {logoPath}");
        }
    }

    // progress: 0..1 draws the progress bar at that fill level; null draws no bar at all.
    public void Draw(SpriteBatch sprites, GraphicsDevice graphics, float? progress = null)
    {
        graphics.Clear(BackgroundColor);

        int screenWidth = graphics.Viewport.Width;
        int screenHeight = graphics.Viewport.Height;

        // PointClamp instead of LinearClamp for crisp, pixel-perfect scaling
        sprites.Begin(samplerState: SamplerState.PointClamp);

        if (_logo != null)
            DrawLogo(sprites, screenWidth, screenHeight);

        if (progress.HasValue)
            DrawProgressBar(sprites, screenWidth, screenHeight, MathHelper.Clamp(progress.Value, 0f, 1f));

        sprites.End();
    }

    private void DrawLogo(SpriteBatch sprites, int screenWidth, int screenHeight)
    {
        float targetSize = screenHeight * LogoHeightFraction;
        float scale = targetSize / _logo.Height;

        int targetWidth = (int)(_logo.Width * scale);
        int targetHeight = (int)(_logo.Height * scale);

        int x = (screenWidth - targetWidth) / 2;
        int y = (screenHeight - targetHeight) / 2;

        sprites.Draw(_logo, new Rectangle(x, y, targetWidth, targetHeight), Color.White);
    }

    private void DrawProgressBar(SpriteBatch sprites, int screenWidth, int screenHeight, float progress)
    {
        // The logo is always centered and LogoHeightFraction of the screen tall, so its bottom edge is
        // known without needing the logo texture. That keeps the bar in place even if the logo is missing.
        float logoBottom = screenHeight * (0.5f + LogoHeightFraction / 2f);

        int barWidth = (int)(screenWidth * BarWidthFraction);
        int barHeight = Math.Max(BarMinHeight, (int)(screenHeight * BarHeightFraction));
        int barX = (screenWidth - barWidth) / 2;
        int barY = (int)(logoBottom + screenHeight * BarGapFraction);

        sprites.Draw(_pixel, new Rectangle(barX, barY, barWidth, barHeight), _barTrackColor);

        int fillWidth = (int)(barWidth * progress);
        if (fillWidth > 0)
            sprites.Draw(_pixel, new Rectangle(barX, barY, fillWidth, barHeight), _barFillColor);
    }
}
