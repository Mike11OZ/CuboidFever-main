
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// The in-game pause menu: a dimmed overlay over the frozen world with four buttons. Like MainMenu it
// decides nothing itself; it raises an event for each button and Game1 decides what happens.
namespace CuboidFever;

public sealed class PauseMenu
{
    private readonly SpriteFont _font;
    private readonly ButtonMenu _buttons;
    private readonly Texture2D _pixel;

    private const string Title = "Game Paused";
    private const float TitleScale = 2f;
    private const float TitleTopFraction = 0.17f;

    // How dark the overlay over the world is (0 = invisible, 1 = solid black)
    private const float DimAmount = 0.6f;

    public event Action ResumeRequested;
    public event Action SettingsRequested;
    public event Action ExitWorldRequested;
    public event Action ExitGameRequested;

    public PauseMenu(
        GraphicsDevice graphics,
        SpriteFont font,
        Action<MenuButtonSound> playMenuSound)
    {
        _font = font;

        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _buttons = new ButtonMenu(graphics, font, playMenuSound)
        {
            Top = 0.35f,
            DesignWidth = 340 // wide enough for "Save and Quit to Title"
        };

        _buttons.Add(
            "Resume",
            () => ResumeRequested?.Invoke(),
            MenuButtonSound.Back
        );

        _buttons.Add("Settings", () => SettingsRequested?.Invoke());

        _buttons.Add(
            "Save and Quit to Title",
            () => ExitWorldRequested?.Invoke(),
            MenuButtonSound.Back
        );

        _buttons.Add("Exit Game", () => ExitGameRequested?.Invoke());
    }

    public void Update(ActionInput input) => _buttons.Update(input);

    // Draws on top of whatever is already on screen (the frozen world), so it does not clear the screen.
    public void Draw(SpriteBatch sprites, GraphicsDevice graphics)
    {
        int screenWidth = graphics.Viewport.Width;
        int screenHeight = graphics.Viewport.Height;

        sprites.Begin(samplerState: SamplerState.PointClamp);

        sprites.Draw(
            _pixel,
            new Rectangle(0, 0, screenWidth, screenHeight),
            Color.Black * DimAmount
        );

        if (_font != null)
        {
            // Drawn at the GUI scale, so the title grows with the buttons
            float titleScale = TitleScale * GuiScale.For(graphics);

            Vector2 titleSize = _font.MeasureString(Title) * titleScale;

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
                titleScale,
                SpriteEffects.None,
                0f
            );
        }

        _buttons.Draw(sprites);

        sprites.End();
    }
}
