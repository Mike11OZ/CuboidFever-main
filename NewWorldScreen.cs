using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// The "Create New World" screen: a name box, a seed box, and Create / Cancel buttons.
//
// Typing arrives through OnTextInput (the window's text-input event, forwarded by Game1 and MainMenu), because
// that is the one place the operating system turns key presses into characters for the player's keyboard layout.
// The editing rules are in TextFieldModel and the meaning of the typed text is in NewWorldRules, both tested.
//
// Like the other menus it holds no game logic: it raises CreateRequested with the raw typed text and
// BackRequested for Cancel / Escape, and Game1 decides what happens.
public sealed class NewWorldScreen
{
    private readonly GraphicsDevice _graphics;
    private readonly SpriteFont _font; // large menu font, for the heading; may be null
    private readonly SpriteFont _text; // small menu font, for labels and what is typed
    private readonly Action<MenuButtonSound> _playSound;
    private readonly Texture2D _pixel;
    private readonly ButtonMenu _buttons;

    private readonly TextFieldModel _name = new(NewWorldRules.MaxNameLength, NewWorldRules.IsTypable);
    private readonly TextFieldModel _seed = new(NewWorldRules.MaxSeedLength, NewWorldRules.IsTypable);

    private int _focus;            // 0 = name box, 1 = seed box
    private float _time;           // drives the blinking caret
    private bool _submit;          // Enter was pressed; handled in Update, not inside the input event
    private string _namePlaceholder = string.Empty;

    public event Action<string, string> CreateRequested; // the name and seed exactly as typed
    public event Action BackRequested;

    private static readonly Color FillColor = new(44, 40, 62);
    private static readonly Color BorderColor = new(90, 84, 122);
    private static readonly Color BorderFocusColor = new(235, 235, 245);
    private static readonly Color TextColor = new(235, 235, 245);
    private static readonly Color DimTextColor = new(150, 144, 180);

    private const int DesignWidth = 360;
    private const int DesignFieldHeight = 36;
    private const int DesignBorder = 2;

    public NewWorldScreen(GraphicsDevice graphics, SpriteFont font, SpriteFont smallFont, Action<MenuButtonSound> playSound)
    {
        _graphics = graphics;
        _font = font;
        _text = smallFont ?? font;
        _playSound = playSound;

        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _buttons = new ButtonMenu(graphics, font, playSound) { DesignWidth = DesignWidth, DesignHeight = 36, DesignGap = 8 };
        _buttons.Add("Create World", () => CreateRequested?.Invoke(_name.Text, _seed.Text), MenuButtonSound.Start);
        _buttons.Add("Cancel", () => BackRequested?.Invoke(), MenuButtonSound.Back);
    }

    // Opens the screen with empty boxes. defaultName ("World 3") is shown faintly in the name box and is what
    // the world is called if the player types nothing.
    public void Show(string defaultName)
    {
        _name.Clear();
        _seed.Clear();
        _focus = 0;
        _time = 0f;
        _submit = false;
        _namePlaceholder = defaultName ?? string.Empty;
    }

    // One character from the window's text-input event.
    public void OnTextInput(char c)
    {
        if (c == '\t')
        {
            _focus = 1 - _focus;
            return;
        }

        if (c == '\r' || c == '\n')
        {
            _submit = true;
            return;
        }

        (_focus == 0 ? _name : _seed).Handle(c);
        _time = 0f; // the caret stays solid while typing
    }

    // =====================  LAYOUT  =====================

    private readonly struct Layout
    {
        public Point NameLabel { get; init; }
        public Rectangle NameBox { get; init; }
        public Point SeedLabel { get; init; }
        public Rectangle SeedBox { get; init; }
        public int ButtonsTop { get; init; }
    }

    private Layout Measure(int scale)
    {
        int screenWidth = _graphics.Viewport.Width;
        int width = Math.Min(DesignWidth * scale, screenWidth - 2 * WorldListLayout.ScreenMargin);
        int x = (screenWidth - width) / 2;

        int labelHeight = ((_text?.LineSpacing ?? 16) + 4) * scale;
        int fieldHeight = DesignFieldHeight * scale;

        int y = 72 * scale; // below the heading, the same height as the world list's header
        var nameBox = new Rectangle(x, y + labelHeight, width, fieldHeight);

        int seedY = nameBox.Bottom + 14 * scale;
        var seedBox = new Rectangle(x, seedY + labelHeight, width, fieldHeight);

        return new Layout
        {
            NameLabel = new Point(x, y),
            NameBox = nameBox,
            SeedLabel = new Point(x, seedY),
            SeedBox = seedBox,
            ButtonsTop = seedBox.Bottom + 24 * scale
        };
    }

    // =====================  UPDATE  =====================

    public void Update(ActionInput input, float deltaTime)
    {
        int scale = GuiScale.For(_graphics);
        Layout layout = Measure(scale);

        _time += deltaTime;

        if (_submit)
        {
            _submit = false;
            _playSound?.Invoke(MenuButtonSound.Start);
            CreateRequested?.Invoke(_name.Text, _seed.Text);
            return;
        }

        if (input.WasLeftClicked)
        {
            if (layout.NameBox.Contains(input.MousePosition)) _focus = 0;
            else if (layout.SeedBox.Contains(input.MousePosition)) _focus = 1;
        }

        // Keep Create / Cancel just below the boxes, wherever the window size has put them
        _buttons.Top = (float)layout.ButtonsTop / _graphics.Viewport.Height;
        _buttons.Update(input); // Escape presses Cancel
    }

    // =====================  DRAW  =====================

    // The caller (MainMenu) has a SpriteBatch open and ends it.
    public void Draw(SpriteBatch sprites)
    {
        int scale = GuiScale.For(_graphics);
        int screenWidth = _graphics.Viewport.Width;
        Layout layout = Measure(scale);

        if (_font != null)
        {
            const string heading = "Create New World";
            Vector2 size = _font.MeasureString(heading) * scale;
            var pos = new Vector2(
                MathF.Round((screenWidth - size.X) / 2f),
                MathF.Round((72 * scale - size.Y) / 2f));

            sprites.DrawString(_font, heading, pos, TextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        DrawLabel(sprites, "World Name", layout.NameLabel, scale);
        DrawField(sprites, layout.NameBox, _name.Text, _namePlaceholder, _focus == 0, scale);

        DrawLabel(sprites, "Seed (blank for random)", layout.SeedLabel, scale);
        DrawField(sprites, layout.SeedBox, _seed.Text, "Random", _focus == 1, scale);

        _buttons.Draw(sprites);
    }

    private void DrawLabel(SpriteBatch sprites, string label, Point at, int scale)
    {
        if (_text == null) return;
        sprites.DrawString(_text, label, new Vector2(at.X, at.Y), DimTextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawField(SpriteBatch sprites, Rectangle box, string text, string placeholder, bool focused, int scale)
    {
        int border = DesignBorder * scale;

        sprites.Draw(_pixel, box, focused ? BorderFocusColor : BorderColor);
        sprites.Draw(_pixel, new Rectangle(box.X + border, box.Y + border, box.Width - border * 2, box.Height - border * 2), FillColor);

        if (_text == null) return;

        int padding = 8 * scale;
        int caretWidth = 2 * scale;
        int room = box.Width - border * 2 - padding * 2 - caretWidth;

        int textY = box.Y + (box.Height - _text.LineSpacing * scale) / 2;
        int textX = box.X + border + padding;

        if (text.Length == 0)
        {
            // Faint hint for what happens if the box is left empty
            if (placeholder.Length > 0)
                sprites.DrawString(_text, Tail(placeholder, room, scale), new Vector2(textX, textY), DimTextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
        else
        {
            sprites.DrawString(_text, Tail(text, room, scale), new Vector2(textX, textY), TextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        // Blinking caret after the text, only in the box being typed in
        if (focused && ((int)(_time * 2f) % 2 == 0))
        {
            int shown = (int)(_text.MeasureString(Tail(text, room, scale)).X * scale);
            var caret = new Rectangle(textX + shown + scale, textY, caretWidth, _text.LineSpacing * scale);
            sprites.Draw(_pixel, caret, TextColor);
        }
    }

    // The end of the text that fits in the given width, so while typing the newest characters stay visible.
    // Everything typed is plain keyboard characters, which the font always has.
    private string Tail(string text, int maxWidth, int scale)
    {
        while (text.Length > 0 && _text.MeasureString(text).X * scale > maxWidth)
            text = text.Substring(1);

        return text;
    }
}
