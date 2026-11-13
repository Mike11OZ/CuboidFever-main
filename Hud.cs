using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// 2D overlay drawn on top of the world: the crosshair, the selected block icon and the health bar.
//
// The health bar takes its position and size from HudLayout (data/cuboidfever/hud_layout.json). The crosshair and
// the block icon still use the constants below and will move over to the layout later.
//
// All sizes here are design pixels (as they would be in an 800x480 window). They are multiplied by
// GuiScale when drawn, so the HUD keeps the same proportions as the menus at any resolution.
public sealed class Hud
{
    private const int IconSize = 64, IconPad = 16, IconBorder = 4;

    // Crosshair: each arm is this long in total (centered on the screen) and this thick
    private const int CrosshairLength = 16, CrosshairThickness = 2;

    // Health bar colors and frame
    private const int HealthBarBorder = 2; // design pixels
    private static readonly Color HealthBarFrame = Color.Black * 0.6f;
    private static readonly Color HealthBarBack = new(50, 10, 10);
    private static readonly Color HealthBarFill = new(200, 30, 30);

    private readonly GraphicsDevice _gd;
    private readonly SpriteBatch _sprites;
    private readonly Texture2D _atlas;
    private readonly Hotbar _hotbar;
    private readonly Texture2D _pixel;
    private readonly HudLayout _layout;

    // The player whose health is shown. Set when a world is entered and cleared when it is left (nothing is drawn without one).
    public Player Player { get; set; }

    public bool Visible { get; private set; } = true;

    public void ToggleVisibility()
    {
        Visible = !Visible;
    }

    public Hud(GraphicsDevice graphicsDevice, SpriteBatch sprites, Texture2D atlas, Hotbar hotbar, HudLayout layout)
    {
        _layout = layout;
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
        DrawHealthBar();

        _sprites.End();
    }

    private void DrawHealthBar()
    {
        if (Player == null) return;

        float scale = GuiScale.For(_gd);
        Rectangle bar = _layout.Resolve(HudElements.HealthBar, _gd.Viewport.Width, _gd.Viewport.Height, scale);
        int border = GuiScale.Pixels(HealthBarBorder * scale);

        // Frame, then the empty part of the bar, then the red fill on top, shrinking from the right as health drops
        _sprites.Draw(_pixel,
            new Rectangle(bar.X - border, bar.Y - border, bar.Width + border * 2, bar.Height + border * 2),
            HealthBarFrame);
        _sprites.Draw(_pixel, bar, HealthBarBack);

        int fillWidth = GuiScale.Pixels(bar.Width * Player.Health.Fraction);
        if (fillWidth > 0)
            _sprites.Draw(_pixel, new Rectangle(bar.X, bar.Y, fillWidth, bar.Height), HealthBarFill);
    }

    private void DrawCrosshair()
    {
        float scale = GuiScale.For(_gd);
        int length = GuiScale.Pixels(CrosshairLength * scale), thickness = GuiScale.Pixels(CrosshairThickness * scale);

        int cx = _gd.Viewport.Width / 2, cy = _gd.Viewport.Height / 2;
        _sprites.Draw(_pixel, new Rectangle(cx - length / 2, cy - thickness / 2, length, thickness), Color.White);
        _sprites.Draw(_pixel, new Rectangle(cx - thickness / 2, cy - length / 2, thickness, length), Color.White);
    }

    private void DrawSelectedBlock()
    {
        var registry = BlockRegistry.Instance;
        byte id = _hotbar.SelectedId;

        float scale = GuiScale.For(_gd);
        int iconSize = GuiScale.Pixels(IconSize * scale), iconPad = GuiScale.Pixels(IconPad * scale), iconBorder = GuiScale.Pixels(IconBorder * scale);

        var src = registry.GetTextureRegion(id, 0).PixelBounds;
        var dest = new Rectangle(iconPad, _gd.Viewport.Height - iconPad - iconSize, iconSize, iconSize);

        _sprites.Draw(_pixel,
            new Rectangle(dest.X - iconBorder, dest.Y - iconBorder, iconSize + iconBorder * 2, iconSize + iconBorder * 2),
            Color.Black * 0.6f);
        _sprites.Draw(_atlas, dest, src, registry.GetTint(id));
    }
}
