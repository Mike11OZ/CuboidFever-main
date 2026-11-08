using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// A "are you sure?" box over a menu screen: a dimmed background, a heading and a message, and two buttons.
// While it is open it should get all the input (MainMenu skips everything else), and Escape presses Cancel.
//
// It holds no game logic: Show is given what to do on each answer.
public sealed class ConfirmDialog
{
    private readonly GraphicsDevice _graphics;
    private readonly SpriteFont _text; // small menu font for the heading and message; may be null
    private readonly Texture2D _pixel;
    private readonly ButtonMenu _buttons;

    private string _heading = string.Empty;
    private string _message = string.Empty;
    private string _confirmLabel = "OK";
    private string _cancelLabel = "Cancel";
    private Action _onConfirm;
    private Action _onCancel;

    private static readonly Color TextColor = new(235, 235, 245);
    private static readonly Color DimTextColor = new(170, 164, 200);

    private const int MaxMessageLines = 5;

    public bool IsOpen { get; private set; }

    public ConfirmDialog(GraphicsDevice graphics, SpriteFont font, SpriteFont smallFont, Action<MenuButtonSound> playSound)
    {
        _graphics = graphics;
        _text = smallFont ?? font;

        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // The labels are read when drawn, so one pair of buttons serves every question
        _buttons = new ButtonMenu(graphics, font, playSound) { Top = 0.52f, DesignWidth = 340 };
        _buttons.Add(() => _confirmLabel, () => Answer(_onConfirm));
        _buttons.Add(() => _cancelLabel, () => Answer(_onCancel), MenuButtonSound.Back);
    }

    public void Show(string heading, string message, string confirmLabel, string cancelLabel, Action onConfirm, Action onCancel)
    {
        _heading = heading ?? string.Empty;
        _message = message ?? string.Empty;
        _confirmLabel = confirmLabel ?? "OK";
        _cancelLabel = cancelLabel ?? "Cancel";
        _onConfirm = onConfirm;
        _onCancel = onCancel;
        IsOpen = true;
    }

    // Closes first, then runs the answer, so the answer is free to open another dialog.
    private void Answer(Action action)
    {
        IsOpen = false;
        action?.Invoke();
    }

    public void Update(ActionInput input)
    {
        if (IsOpen) _buttons.Update(input); // Escape presses Cancel
    }

    // The caller has a SpriteBatch open.
    public void Draw(SpriteBatch sprites)
    {
        if (!IsOpen) return;

        int scale = GuiScale.For(_graphics);
        int screenWidth = _graphics.Viewport.Width;
        int screenHeight = _graphics.Viewport.Height;

        sprites.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight), Color.Black * 0.75f);

        if (_text != null)
        {
            int maxWidth = Math.Min(560 * scale, screenWidth - 2 * WorldListLayout.ScreenMargin);
            var lines = TextWrap.Wrap(_message, s => _text.MeasureString(s).X * scale, maxWidth, MaxMessageLines);

            int lineHeight = (_text.LineSpacing + 6) * scale;
            int bottom = (int)(screenHeight * 0.52f) - 16 * scale;        // just above the buttons
            int top = bottom - (lines.Count + 1) * lineHeight;             // heading plus the message lines

            DrawCentered(sprites, _heading, screenWidth, top, scale, TextColor);

            for (int i = 0; i < lines.Count; i++)
                DrawCentered(sprites, lines[i], screenWidth, top + (i + 1) * lineHeight, scale, DimTextColor);
        }

        _buttons.Draw(sprites);
    }

    private void DrawCentered(SpriteBatch sprites, string text, int screenWidth, int y, int scale, Color color)
    {
        Vector2 size = _text.MeasureString(text) * scale;
        var pos = new Vector2(MathF.Round((screenWidth - size.X) / 2f), y);

        sprites.DrawString(_text, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
