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
    private sealed record Button(string Label, Action OnClick);

    private readonly GraphicsDevice _graphics;
    private readonly SpriteFont _font; // may be null if no font could be loaded; buttons then draw without text
    private readonly Texture2D _pixel;
    private readonly List<Button> _buttons = new();

    private int _hovered = -1;

    // Where the top of the first button sits, as a fraction of the screen height
    public float Top { get; set; } = 0.45f;

    // Colors, matching the loading screen's dark purple palette
    private static readonly Color FillColor = new(44, 40, 62);
    private static readonly Color FillHoverColor = new(72, 66, 102);
    private static readonly Color BorderColor = new(90, 84, 122);
    private static readonly Color BorderHoverColor = new(235, 235, 245);
    private static readonly Color TextColor = new(235, 235, 245);

    private const int BorderThickness = 2;

    public ButtonMenu(GraphicsDevice graphics, SpriteFont font)
    {
        _graphics = graphics;
        _font = font;
        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Add(string label, Action onClick) => _buttons.Add(new Button(label, onClick));

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

        // The click may switch screens, so nothing else should run in this menu after it
        if (_hovered >= 0 && input.WasLeftClicked)
            _buttons[_hovered].OnClick();
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

            var inner = new Rectangle(rect.X + BorderThickness, rect.Y + BorderThickness,
                                      rect.Width - BorderThickness * 2, rect.Height - BorderThickness * 2);
            sprites.Draw(_pixel, inner, hovered ? FillHoverColor : FillColor);

            if (_font == null) continue;

            // Whole pixels only, so the text isn't smeared across pixel boundaries
            Vector2 size = _font.MeasureString(_buttons[i].Label);
            var pos = new Vector2((int)(rect.X + (rect.Width - size.X) / 2f),
                                  (int)(rect.Y + (rect.Height - size.Y) / 2f));
            sprites.DrawString(_font, _buttons[i].Label, pos, TextColor);
        }
    }

    // Layout is recomputed from the current viewport so the menu follows window resizes.
    private Rectangle GetRect(int index)
    {
        int screenWidth = _graphics.Viewport.Width;
        int screenHeight = _graphics.Viewport.Height;

        int width = Math.Max(260, (int)(screenWidth * 0.25f));
        int height = Math.Max(48, (int)(screenHeight * 0.075f));
        int gap = height / 3;

        int x = (screenWidth - width) / 2;
        int y = (int)(screenHeight * Top) + index * (height + gap);

        return new Rectangle(x, y, width, height);
    }
}
