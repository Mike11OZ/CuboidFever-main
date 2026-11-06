
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// The title screen: the game's name and three buttons. It decides nothing itself; it raises an event
// for each button and Game1 decides what happens (start the game, open settings, quit).
public sealed class MainMenu
{
    private readonly SpriteFont _font;
    private readonly ButtonMenu _buttons;
    private readonly Texture2D _backgroundTexture;

    private const float TitleScale = 2f;
    private const float TitleTopFraction = 0.18f;

    public event Action PlayRequested;
    public event Action SettingsRequested;
    public event Action ExitRequested;

    public MainMenu(
        GraphicsDevice graphics,
        SpriteFont font,
        Action<MenuButtonSound> playMenuSound)
    {
        _font = font;

        _backgroundTexture = TextureLoader.Load(
            graphics,
            "cuboidfever/textures/gui/main_menu_bg.png"
        );

        // Pass the audio callback to the shared button menu.
        _buttons = new ButtonMenu(graphics, font, playMenuSound);

        // All three buttons use the default click sound.
        _buttons.Add(
            "Play Now",
            () => PlayRequested?.Invoke(),
            MenuButtonSound.Start
        );
        _buttons.Add("Settings", () => SettingsRequested?.Invoke());
        _buttons.Add("Exit Game", () => ExitRequested?.Invoke());
    }

    public void Update(ActionInput input) => _buttons.Update(input);

    public void Draw(SpriteBatch sprites, GraphicsDevice graphics)
    {
        int screenWidth = graphics.Viewport.Width;
        int screenHeight = graphics.Viewport.Height;

        graphics.Clear(LoadingScreen.BackgroundColor);

        // Background.
        sprites.Begin(samplerState: SamplerState.LinearClamp);

        if (_backgroundTexture != null)
        {
            var destinationRectangle = new Rectangle(
                0, 0, screenWidth, screenHeight
            );

            sprites.Draw(_backgroundTexture, destinationRectangle, Color.White);
        }

        sprites.End();

        // Pixel-style UI text and buttons.
        sprites.Begin(samplerState: SamplerState.PointClamp);

        if (_font != null)
        {
            // Everything is drawn at the GUI scale, so the title and version text grow with the buttons
            int scale = GuiScale.For(graphics);
            float titleScale = TitleScale * scale;

            Vector2 titleSize = _font.MeasureString(GameInfo.Title);

            var titlePos = new Vector2(
                MathF.Round((screenWidth - titleSize.X * titleScale) / 2f),
                MathF.Round(screenHeight * TitleTopFraction)
            );

            sprites.DrawString(
                _font,
                GameInfo.Title,
                titlePos,
                Color.White,
                0f,
                Vector2.Zero,
                titleScale,
                SpriteEffects.None,
                0f
            );

            var versionPos = new Vector2(
                12 * scale,
                screenHeight - (_font.LineSpacing + 8) * scale
            );

            sprites.DrawString(
                _font,
                $"{GameInfo.Version} ({GameInfo.BuildType})",
                versionPos,
                new Color(90, 84, 122),
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f
            );
        }

        _buttons.Draw(sprites);

        sprites.End();
    }
}
