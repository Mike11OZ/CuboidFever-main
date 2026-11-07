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

    // Button width in design pixels (as it would be in an 800x480 window). Menus with long labels raise it
    // to fit their longest text. Like every size here it is multiplied by GuiScale when drawn.
    public int DesignWidth { get; set; } = 260;

    // Button height and the space between buttons, also in design pixels. A menu with many buttons
    // (settings) makes these smaller so everything still fits in a small window.
    public int DesignHeight { get; set; } = 48;
    public int DesignGap { get; set; } = 16;

    // Never let a button reach the screen edges, however small the window gets
    private const int ScreenMargin = 20;

    // Colors, matching the loading screen's dark purple palette.
    private static readonly Color FillColor = new(44, 40, 62);
    private static readonly Color FillHoverColor = new(72, 66, 102);
    private static readonly Color BorderColor = new(90, 84, 122);
    private static readonly Color BorderHoverColor = new(235, 235, 245);
    private static readonly Color TextColor = new(235, 235, 245);

    private const int DesignBorder = 2;

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

    // Removes every button, so a menu whose contents change (the list of saved worlds) can be rebuilt.
    public void Clear()
    {
        _buttons.Clear();
        _hovered = -1;
        _pressed = -1;
    }

    /// <summary>
    /// Total height of all buttons + gaps, in pixels, at the given GUI scale.
    /// Used by menus that need to vertically center the whole stack.
    /// </summary>
    public float GetTotalHeight(int scale)
    {
        if (_buttons.Count == 0) return 0f;
        return (DesignHeight * _buttons.Count + DesignGap * (_buttons.Count - 1)) * scale;
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
                ActivateButton(pressed);
            }
        }

        // Escape / GameAction.Pause automatically activates the Back button (if one exists).
        // This gives every menu correct "go back one level" behaviour for free.
        if (input.WasPressed(GameAction.Pause))
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i].Sound == MenuButtonSound.Back)
                {
                    ActivateButton(i);
                    break;
                }
            }
        }
    }

    // Shared path for both mouse clicks and Escape so the sound and action stay identical.
    private void ActivateButton(int index)
    {
        Button button = _buttons[index];

        if (button.Sound != MenuButtonSound.None)
            _playSound?.Invoke(button.Sound);

        button.OnClick();
    }

    // The caller owns sprites.Begin/End so it can draw other things (a title) in the same batch.
    public void Draw(SpriteBatch sprites)
    {
        // The text is drawn at the same scale as the button, so the two always keep the same proportions.
        int scale = GuiScale.For(_graphics);
        int borderThickness = DesignBorder * scale;

        for (int i = 0; i < _buttons.Count; i++)
        {
            Rectangle rect = GetRect(i);
            bool hovered = i == _hovered;

            Color border = hovered ? BorderHoverColor : BorderColor;
            sprites.Draw(_pixel, rect, border);

            var inner = new Rectangle(
                rect.X + borderThickness,
                rect.Y + borderThickness,
                rect.Width - borderThickness * 2,
                rect.Height - borderThickness * 2
            );

            sprites.Draw(_pixel, inner, hovered ? FillHoverColor : FillColor);

            if (_font == null) continue;

            // Whole pixels only, so the text isn't smeared across pixel boundaries.
            string label = _buttons[i].Label();
            Vector2 size = _font.MeasureString(label) * scale;

            var pos = new Vector2(
                (int)(rect.X + (rect.Width - size.X) / 2f),
                (int)(rect.Y + rect.Height / 2f - _capCenterY * scale)
            );

            sprites.DrawString(_font, label, pos, TextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }

    // Layout is recomputed from the current viewport so the menu follows window resizes.
    private Rectangle GetRect(int index)
    {
        int screenWidth = _graphics.Viewport.Width;
        int screenHeight = _graphics.Viewport.Height;

        int scale = GuiScale.For(_graphics);

        int width = Math.Min(DesignWidth * scale, screenWidth - ScreenMargin * 2);
        int height = DesignHeight * scale;
        int gap = DesignGap * scale;

        int x = (screenWidth - width) / 2;
        int y = (int)(screenHeight * Top) + index * (height + gap);

        return new Rectangle(x, y, width, height);
    }
}