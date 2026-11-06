using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// 2D overlay drawn on top of the world: the crosshair and the selected block icon.
//
// All sizes here are design pixels (as they would be in an 800x480 window). They are multiplied by
// GuiScale when drawn, so the HUD keeps the same proportions as the menus at any resolution.
public sealed class Hud
{
    private const int IconSize = 64, IconPad = 16, IconBorder = 4;

    // Crosshair: each arm is this long in total (centered on the screen) and this thick
    private const int CrosshairLength = 16, CrosshairThickness = 2;

    private readonly GraphicsDevice _gd;
    private readonly SpriteBatch _sprites;
    private readonly Texture2D _atlas;
    private readonly Hotbar _hotbar;
    private readonly Texture2D _pixel;

    public bool Visible { get; private set; } = true;

    public void ToggleVisibility()
    {
        Visible = !Visible;
    }

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
        if (!Visible)
            return;

        _sprites.Begin(samplerState: SamplerState.PointClamp);

        DrawCrosshair();
        DrawSelectedBlock();

        _sprites.End();
    }

    private void DrawCrosshair()
    {
        int scale = GuiScale.For(_gd);
        int length = CrosshairLength * scale, thickness = CrosshairThickness * scale;

        int cx = _gd.Viewport.Width / 2, cy = _gd.Viewport.Height / 2;
        _sprites.Draw(_pixel, new Rectangle(cx - length / 2, cy - thickness / 2, length, thickness), Color.White);
        _sprites.Draw(_pixel, new Rectangle(cx - thickness / 2, cy - length / 2, thickness, length), Color.White);
    }

    private void DrawSelectedBlock()
    {
        var registry = BlockRegistry.Instance;
        byte id = _hotbar.SelectedId;

        int scale = GuiScale.For(_gd);
        int iconSize = IconSize * scale, iconPad = IconPad * scale, iconBorder = IconBorder * scale;

        var src = registry.GetTextureRegion(id, 0).PixelBounds;
        var dest = new Rectangle(iconPad, _gd.Viewport.Height - iconPad - iconSize, iconSize, iconSize);

        _sprites.Draw(_pixel,
            new Rectangle(dest.X - iconBorder, dest.Y - iconBorder, iconSize + iconBorder * 2, iconSize + iconBorder * 2),
            Color.Black * 0.6f);
        _sprites.Draw(_atlas, dest, src, registry.GetTint(id));
    }
}
