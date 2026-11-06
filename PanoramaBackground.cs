using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

/// <summary>
/// Minecraft-style scrolling cylindrical panorama.
/// Designed to be reused on any screen (title, pause, loading, etc.).
/// </summary>
public sealed class PanoramaBackground
{
    private readonly Texture2D _texture;
    private float _scroll; // 0 → 1

    /// <summary>How fast the panorama rotates (fraction of full 360° per second).</summary>
    public float ScrollSpeed { get; set; } = 0.012f;

    /// <summary>Optional vertical offset in pixels (positive = move panorama up).</summary>
    public float VerticalOffset { get; set; } = 0f;

    public PanoramaBackground(GraphicsDevice graphics, string assetPath)
    {
        _texture = TextureLoader.Load(graphics, assetPath);
    }

    public void Update(float deltaTime)
    {
        if (_texture == null) return;

        _scroll += ScrollSpeed * deltaTime;
        // Keep it in [0, 1)
        _scroll -= MathF.Floor(_scroll);
    }

    public void Draw(SpriteBatch sprites, GraphicsDevice graphics)
    {
        if (_texture == null) return;

        int screenW = graphics.Viewport.Width;
        int screenH = graphics.Viewport.Height;

        // Scale the panorama so its height matches the screen (common Minecraft look).
        // You can change this multiplier if you want letterboxing or extra sky/ground.
        float scale = (float)screenH / _texture.Height;
        int scaledWidth  = (int)(_texture.Width * scale);
        int scaledHeight = screenH;

        // Horizontal scroll in pixels
        float offsetX = _scroll * scaledWidth;

        // Because the texture is a full 360°, we only ever need to draw two copies
        // when the scroll crosses the edge.
        sprites.Begin(samplerState: SamplerState.LinearClamp, blendState: BlendState.Opaque);

        // First segment
        DrawSegment(sprites, -offsetX, scaledWidth, scaledHeight, screenW, screenH);

        // Second segment (wrap-around)
        DrawSegment(sprites, -offsetX + scaledWidth, scaledWidth, scaledHeight, screenW, screenH);

        sprites.End();
    }

    private void DrawSegment(SpriteBatch sprites, float x, int scaledWidth, int scaledHeight,
                             int screenW, int screenH)
    {
        // Only draw if this segment is at least partially visible
        if (x > screenW || x + scaledWidth < 0)
            return;

        var dest = new Rectangle(
            (int)MathF.Round(x),
            (int)MathF.Round(VerticalOffset),
            scaledWidth,
            scaledHeight
        );

        sprites.Draw(_texture, dest, Color.White);
    }
}