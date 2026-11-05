
using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// The settings screen: one button per option that shows its current value and cycles to the next value
// when clicked, plus a Back button.
public sealed class SettingsMenu
{
    private readonly SpriteFont _font;
    private readonly ButtonMenu _buttons;
    private readonly Texture2D _pixel;

    private const string Title = "Settings";
    private const float TitleScale = 2f;
    private const float TitleTopFraction = 0.10f;

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
        _font = font;

        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _buttons = new ButtonMenu(graphics, font, playMenuSound)
        {
            Top = 0.25f,
            WidthFraction = 0.4f
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
            };

            _buttons.MinWidth = (int)longest.Max(s => font.MeasureString(s).X) + 60;
        }
    }

    // 0 draws every leaf face (no culling); higher numbers cull more aggressively.
    private static string LeafCullLabel(int depth) =>
        depth <= 0 ? "Off" : $"Depth {depth}";

    public void Update(ActionInput input) => _buttons.Update(input);

    // overWorld: true when opened from the pause menu. The frozen world has already been drawn,
    // so this dims it instead of clearing the screen.
    public void Draw(SpriteBatch sprites, GraphicsDevice graphics, bool overWorld)
    {
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
            Vector2 titleSize = _font.MeasureString(Title) * TitleScale;

            var titlePos = new Vector2(
                MathF.Round((screenWidth - titleSize.X) / 2f),
                MathF.Round(screenHeight * TitleTopFraction)
            );

            sprites.DrawString(
                _font,
                Title,
                titlePos,
                Color.White,
                0f,
                Vector2.Zero,
                TitleScale,
                SpriteEffects.None,
                0f
            );
        }

        _buttons.Draw(sprites);

        sprites.End();
    }
}
