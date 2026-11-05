
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// A vertical, horizontally centered stack of clickable buttons. It holds no game logic: each button
// just runs the Action it was added with. The main menu, pause menu and settings menu all build on this
// so they share one look and one way of handling the mouse.
//
// Update needs the mouse cursor to be free (InputManager.SetMouseCaptured(false)).
public sealed class ButtonMenu
{
    // The label is a function so a button can show a value that changes ("Window Mode: Borderless").
    private sealed record Button(
        Func<string> Label,
        Action OnClick,
        MenuButtonSound Sound
    );

    private readonly GraphicsDevice _graphics;
    private readonly SpriteFont _font; // may be null if no font could be loaded
    private readonly Texture2D _pixel;
    private readonly List<Button> _buttons = new();
    private readonly Action<MenuButtonSound> _playSound;

    // How far below the top of a text line the middle of the capital letters is.
    private readonly float _capCenterY;

    private int _hovered = -1;
    private int _pressed = -1;

    // Where the top of the first button sits, as a fraction of the screen height.
    public float Top { get; set; } = 0.45f;

    // Button width: this fraction of the screen width, but never narrower than MinWidth pixels.
    public float WidthFraction { get; set; } = 0.25f;
    public int MinWidth { get; set; } = 260;

    // Colors, matching the loading screen's dark purple palette.
    private static readonly Color FillColor = new(44, 40, 62);
    private static readonly Color FillHoverColor = new(72, 66, 102);
    private static readonly Color BorderColor = new(90, 84, 122);
    private static readonly Color BorderHoverColor = new(235, 235, 245);
    private static readonly Color TextColor = new(235, 235, 245);

    private const int BorderThickness = 2;

    public ButtonMenu(
        GraphicsDevice graphics,
        SpriteFont font,
        Action<MenuButtonSound> playSound = null)
    {
        _graphics = graphics;
        _font = font;
        _playSound = playSound;

        _capCenterY = MeasureCapCenterY(font);

        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    // Finds the vertical middle of a capital letter inside the font's line.
    // Falls back to the middle of the line if the font has no 'H'.
    private static float MeasureCapCenterY(SpriteFont font)
    {
        if (font == null) return 0f;

        if (font.GetGlyphs().TryGetValue('H', out SpriteFont.Glyph glyph))
            return glyph.Cropping.Y + glyph.BoundsInTexture.Height / 2f;

        return font.LineSpacing / 2f;
    }

    // Adds a static-label button. Click sound is used by default.
    public void Add(
        string label,
        Action onClick,
        MenuButtonSound sound = MenuButtonSound.Click)
    {
        _buttons.Add(new Button(() => label, onClick, sound));
    }

    // Adds a dynamic-label button, such as one that displays the current setting.
    public void Add(
        Func<string> label,
        Action onClick,
        MenuButtonSound sound = MenuButtonSound.Click)
    {
        _buttons.Add(new Button(label, onClick, sound));
    }

    // Call once per frame, after ActionInput.Update.
    public void Update(ActionInput input)
    {
        _hovered = -1;

        Point mouse = input.MousePosition;

        for (int i = 0; i < _buttons.Count; i++)
        {
            if (GetRect(i).Contains(mouse))
            {
                _hovered = i;
                break;
            }
        }

        // A button fires when the mouse is released over the same button it was pressed on.
        // Firing on the press instead could leave the button held as the game resumes.
        if (input.WasLeftClicked)
            _pressed = _hovered;

        if (input.WasLeftReleased)
        {
            int pressed = _pressed;
            _pressed = -1;

            // The click may switch screens, so nothing else should run in this menu after it.
            if (pressed >= 0 && pressed == _hovered)
            {
                Button button = _buttons[pressed];

                if (button.Sound != MenuButtonSound.None)
                    _playSound?.Invoke(button.Sound);

                button.OnClick();
            }
        }
    }

    // The caller owns sprites.Begin/End so it can draw other things (a title) in the same batch.
    public void Draw(SpriteBatch sprites)
    {
        for (int i = 0; i < _buttons.Count; i++)
        {
            Rectangle rect = GetRect(i);
            bool hovered = i == _hovered;

            Color border = hovered ? BorderHoverColor : BorderColor;
            sprites.Draw(_pixel, rect, border);

            var inner = new Rectangle(
                rect.X + BorderThickness,
                rect.Y + BorderThickness,
                rect.Width - BorderThickness * 2,
                rect.Height - BorderThickness * 2
            );

            sprites.Draw(_pixel, inner, hovered ? FillHoverColor : FillColor);

            if (_font == null) continue;

            // Whole pixels only, so the text isn't smeared across pixel boundaries.
            string label = _buttons[i].Label();
            Vector2 size = _font.MeasureString(label);

            var pos = new Vector2(
                (int)(rect.X + (rect.Width - size.X) / 2f),
                (int)(rect.Y + rect.Height / 2f - _capCenterY)
            );

            sprites.DrawString(_font, label, pos, TextColor);
        }
    }

    // Layout is recomputed from the current viewport so the menu follows window resizes.
    private Rectangle GetRect(int index)
    {
        int screenWidth = _graphics.Viewport.Width;
        int screenHeight = _graphics.Viewport.Height;

        int width = Math.Min(
            Math.Max(MinWidth, (int)(screenWidth * WidthFraction)),
            screenWidth - 40
        );

        int height = Math.Max(48, (int)(screenHeight * 0.075f));
        int gap = height / 3;

        int x = (screenWidth - width) / 2;
        int y = (int)(screenHeight * Top) + index * (height + gap);

        return new Rectangle(x, y, width, height);
    }
}
