using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// 2D overlay drawn on top of the world: the crosshair and the selected block icon.
public sealed class Hud
{
    private const int IconSize = 64, IconPad = 16, IconBorder = 4;

    private readonly GraphicsDevice _gd;
    private readonly SpriteBatch _sprites;
    private readonly Texture2D _atlas;
    private readonly Hotbar _hotbar;
    private readonly Texture2D _pixel;

    public Hud(GraphicsDevice graphicsDevice, SpriteBatch sprites, Texture2D atlas, Hotbar hotbar)
    {
        _gd = graphicsDevice;
        _sprites = sprites;
        _atlas = atlas;
        _hotbar = hotbar;

        _pixel = new Texture2D(_gd, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Draw()
    {
        _sprites.Begin(samplerState: SamplerState.PointClamp);

        DrawCrosshair();
        DrawSelectedBlock();

        _sprites.End();
    }

    private void DrawCrosshair()
    {
        int cx = _gd.Viewport.Width / 2, cy = _gd.Viewport.Height / 2;
        _sprites.Draw(_pixel, new Rectangle(cx - 8, cy - 1, 16, 2), Color.White);
        _sprites.Draw(_pixel, new Rectangle(cx - 1, cy - 8, 2, 16), Color.White);
    }

    private void DrawSelectedBlock()
    {
        var registry = BlockRegistry.Instance;
        byte id = _hotbar.SelectedId;

        var src = registry.GetTextureRegion(id, 0).PixelBounds;
        var dest = new Rectangle(IconPad, _gd.Viewport.Height - IconPad - IconSize, IconSize, IconSize);

        _sprites.Draw(_pixel,
            new Rectangle(dest.X - IconBorder, dest.Y - IconBorder, IconSize + IconBorder * 2, IconSize + IconBorder * 2),
            Color.Black * 0.6f);
        _sprites.Draw(_atlas, dest, src, registry.GetTint(id));
    }
}
