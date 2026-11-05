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

    private const float TitleScale = 2.5f;
    private const float TitleTopFraction = 0.18f;

    public event Action PlayRequested;
    public event Action SettingsRequested;
    public event Action ExitRequested;

    public MainMenu(GraphicsDevice graphics, SpriteFont font)
    {
        _font = font;

        _buttons = new ButtonMenu(graphics, font);
        _buttons.Add("Play Now", () => PlayRequested?.Invoke());
        _buttons.Add("Settings", () => SettingsRequested?.Invoke());
        _buttons.Add("Exit Game", () => ExitRequested?.Invoke());
    }

    public void Update(ActionInput input) => _buttons.Update(input);

    public void Draw(SpriteBatch sprites, GraphicsDevice graphics)
    {
        // Same background as the loading screen, so going between them doesn't flash
        graphics.Clear(LoadingScreen.BackgroundColor);

        int screenWidth = graphics.Viewport.Width;
        int screenHeight = graphics.Viewport.Height;

        // PointClamp keeps the enlarged title crisp and pixelated like the rest of the game
        sprites.Begin(samplerState: SamplerState.PointClamp);

        if (_font != null)
        {
            Vector2 titleSize = _font.MeasureString(GameInfo.Title) * TitleScale;
            var titlePos = new Vector2((int)((screenWidth - titleSize.X) / 2f), (int)(screenHeight * TitleTopFraction));
            sprites.DrawString(_font, GameInfo.Title, titlePos, Color.White, 0f, Vector2.Zero, TitleScale, SpriteEffects.None, 0f);

            // Version in the bottom-left corner
            var versionPos = new Vector2(12, screenHeight - _font.LineSpacing - 8);
            sprites.DrawString(_font, $"{GameInfo.Version} ({GameInfo.BuildType})", versionPos, new Color(90, 84, 122));
        }

        _buttons.Draw(sprites);

        sprites.End();
    }
}
