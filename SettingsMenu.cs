
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// The settings screen: one button per option that shows its current value and cycles to the next value
// when clicked, plus a Back button.
public sealed class SettingsMenu
{
    private readonly GraphicsDevice _graphics;
    private readonly SpriteFont _font;
    private readonly ButtonMenu _buttons;
    private readonly Texture2D _pixel;

    private const string Title = "Settings";
    private const float TitleScale = 2f;

    // Space above the title and between the title and the buttons, in design pixels (see GuiScale)
    private const int DesignMargin = 12;

    // This menu has the most buttons, so they are a little shorter and closer together than in the
    // other menus. That is what lets all of them, plus the title, fit in the smallest window.
    private const int DesignButtonHeight = 40;
    private const int DesignButtonGap = 12;

    // Top of the title in pixels. Set by UpdateLayout, which also places the buttons under it.
    private float _titleY;

    // How dark the overlay is when the menu is shown over the paused world.
    private const float DimAmount = 0.6f;

    public event Action BackRequested;

    public SettingsMenu(
        GraphicsDevice graphics,
        SpriteFont font,
        FrameRateLimiter fps,
        WindowManager window,
        Action cycleRenderDistance,
        Action meshSettingChanged,
        Action<MenuButtonSound> playMenuSound)
    {
        _graphics = graphics;
        _font = font;

        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Where the buttons start depends on the title's height, so UpdateLayout sets Top every frame
        _buttons = new ButtonMenu(graphics, font, playMenuSound)
        {
            DesignHeight = DesignButtonHeight,
            DesignGap = DesignButtonGap
        };

        _buttons.Add(() => $"Ambient Occlusion: {(ChunkMesher.AmbientOcclusion ? "On" : "Off")}", () =>
        {
            ChunkMesher.AmbientOcclusion = !ChunkMesher.AmbientOcclusion;
            meshSettingChanged();
        });

        _buttons.Add(() => $"Leaf Culling: {LeafCullLabel(ChunkMesher.LeafCullDepth)}", () =>
        {
            ChunkMesher.LeafCullDepth = (ChunkMesher.LeafCullDepth + 1) % (ChunkMesher.MaxLeafDepth + 1);
            meshSettingChanged();
        });

        _buttons.Add(() => $"Render Distance: {World.RenderDistanceSetting} chunks", cycleRenderDistance);

        _buttons.Add(
            () => $"Frame Rate Limit: {(fps.CurrentCap == 0 ? "Unlimited" : fps.CurrentCap.ToString())}",
            fps.CycleNext
        );

        _buttons.Add(
            () => $"Window Mode: {window.CurrentMode}",
            window.CycleNextMode
        );

        _buttons.Add(
            () => $"GUI Scale: {GuiScale.Label(graphics)}",
            () => GuiScale.CycleSetting(graphics)
        );

        // Back uses its own sound rather than the standard click sound.
        _buttons.Add(
            "Back",
            () => BackRequested?.Invoke(),
            MenuButtonSound.Back
        );

        // Make the buttons wide enough for the longest text each one can show.
        if (font != null)
        {
            string[] longest =
            {
                "Ambient Occlusion: Off",
                "Leaf Culling: Depth 3",
                "Render Distance: 32 chunks",
                "Frame Rate Limit: Unlimited",
                "Window Mode: Borderless",
                "GUI Scale: Auto (4)",
            };

            // Measured at scale 1, which is what DesignWidth is in. ButtonMenu multiplies it by the GUI
            // scale, and the text is drawn at the same scale, so the fit holds at every scale.
            _buttons.DesignWidth = (int)longest.Max(s => font.MeasureString(s).X) + 60;
        }
    }

    // 0 draws every leaf face (no culling); higher numbers cull more aggressively.
    private static string LeafCullLabel(int depth) =>
        depth <= 0 ? "Off" : $"Depth {depth}";

    // Puts the title at the top and the buttons directly under it, both sized by the GUI scale. Done every
    // frame because the scale (and so the title's height) changes with the window size and the GUI Scale
    // setting. Placing the buttons from the title's measured height means they can never overlap it.
    private void UpdateLayout()
    {
        int scale = GuiScale.For(_graphics);

        _titleY = DesignMargin * scale;

        float titleHeight = _font != null ? _font.LineSpacing * TitleScale * scale : 0f;
        float buttonsTop = _titleY + titleHeight + DesignMargin * scale;

        _buttons.Top = buttonsTop / _graphics.Viewport.Height;
    }

    public void Update(ActionInput input)
    {
        UpdateLayout();
        _buttons.Update(input);
    }

    // overWorld: true when opened from the pause menu. The frozen world has already been drawn,
    // so this dims it instead of clearing the screen.
    public void Draw(SpriteBatch sprites, GraphicsDevice graphics, bool overWorld)
    {
        UpdateLayout();

        if (!overWorld)
            graphics.Clear(LoadingScreen.BackgroundColor);

        int screenWidth = graphics.Viewport.Width;
        int screenHeight = graphics.Viewport.Height;

        sprites.Begin(samplerState: SamplerState.PointClamp);

        if (overWorld)
        {
            sprites.Draw(
                _pixel,
                new Rectangle(0, 0, screenWidth, screenHeight),
                Color.Black * DimAmount
            );
        }

        if (_font != null)
        {
            // Drawn at the GUI scale, so the title grows with the buttons
            float titleScale = TitleScale * GuiScale.For(graphics);

            Vector2 titleSize = _font.MeasureString(Title) * titleScale;

            var titlePos = new Vector2(
                MathF.Round((screenWidth - titleSize.X) / 2f),
                MathF.Round(_titleY)
            );

            sprites.DrawString(
                _font,
                Title,
                titlePos,
                Color.White,
                0f,
                Vector2.Zero,
                titleScale,
                SpriteEffects.None,
                0f
            );
        }

        _buttons.Draw(sprites);

        sprites.End();
    }
}
