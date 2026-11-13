using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CuboidFever;

// The in-game command console, like chat in other voxel games: T (or /) opens a line to type in at the bottom
// of the screen, Enter runs it, Esc cancels. While it is open the keyboard is the console's (ActionInput.Blocked
// is set by Game1), but the world keeps running. Answers stay on screen for a few seconds after it closes.
//
// What the commands do lives in commands/. This class is only the typing box and the text log.
public sealed class GameConsole : ICommandSink
{
    private const int MaxLogLines = 100;
    private const int MaxHistory = 50;
    private const int MaxInputLength = 200;
    private const int MaxRowsPerMessage = 4;
    private const int RowsWhenOpen = 12;
    private const int RowsWhenClosed = 8;
    private const float ShowSeconds = 6f;   // how long an answer stays up after the console closes
    private const float FadeSeconds = 1f;

    private static readonly Color ReplyColor = Color.White;
    private static readonly Color ErrorColor = new(255, 110, 110);
    private static readonly Color EchoColor = new(170, 170, 170);

    private sealed class LogLine
    {
        public string Text = "";
        public Color Color;
        public float Age; // seconds since it was added
    }

    private readonly GraphicsDevice _gd;
    private readonly SpriteFont _font;
    private readonly CommandRegistry _registry;
    private readonly CommandContext _context;
    private readonly Texture2D _pixel;

    // Printable ASCII only: the font has no glyphs for anything else, and drawing a missing glyph throws
    private readonly TextFieldModel _field = new(MaxInputLength, c => c >= ' ' && c <= '~');

    private readonly List<LogLine> _log = new();
    private readonly List<string> _history = new(); // oldest first
    private int _historyIndex;                       // == _history.Count when not browsing
    private string _draft = "";                      // what was typed before Up was pressed
    private KeyboardState _prevKb;
    private float _time;

    public bool IsOpen { get; private set; }

    public GameConsole(GraphicsDevice gd, SpriteFont font, CommandRegistry registry, CommandContext context)
    {
        _gd = gd;
        _font = font;
        _registry = registry;
        _context = context;

        _pixel = new Texture2D(gd, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    // =====================  OUTPUT  =====================

    public void Reply(string text) => Add(text, ReplyColor);
    public void Error(string text) => Add(text, ErrorColor);

    private void Add(string text, Color color)
    {
        foreach (string part in (text ?? "").Split('\n'))
        {
            if (part.Length == 0) continue;
            _log.Add(new LogLine { Text = part, Color = color });
        }

        if (_log.Count > MaxLogLines) _log.RemoveRange(0, _log.Count - MaxLogLines);
    }

    // =====================  INPUT  =====================

    // Typed characters, from the window's text-input event. A backspace arrives as '\b'.
    public void OnTextInput(char c)
    {
        if (IsOpen) _field.Handle(c);
    }

    // Call once per frame while playing, before anything else reads input.
    public void Update(float dt, KeyboardState kb, ActionInput input)
    {
        _time += dt;
        foreach (var line in _log) line.Age += dt;

        bool Pressed(Keys k) => kb.IsKeyDown(k) && !_prevKb.IsKeyDown(k);

        if (!IsOpen)
        {
            // The key that opens the console also produces a typed character. That character arrived before this
            // update, while the console was still closed, so it was ignored and does not end up in the box.
            if (input.WasPressed(GameAction.OpenConsole)) Open("");
            else if (input.WasPressed(GameAction.OpenCommandConsole)) Open("/");
        }
        else if (Pressed(Keys.Escape))
        {
            Close();
        }
        else if (Pressed(Keys.Enter))
        {
            Submit();
        }
        else if (Pressed(Keys.Up))
        {
            BrowseHistory(-1);
        }
        else if (Pressed(Keys.Down))
        {
            BrowseHistory(+1);
        }
        else if (Pressed(Keys.Tab))
        {
            TabComplete();
        }

        _prevKb = kb;
    }

    private void Open(string startText)
    {
        IsOpen = true;
        _field.Text = startText;
        _historyIndex = _history.Count;
        _time = 0f; // so the caret starts visible
    }

    private void Close()
    {
        IsOpen = false;
        _field.Clear();
    }

    private void Submit()
    {
        string line = _field.Text.Trim();
        Close();
        if (line.Length == 0) return;

        if (_history.Count == 0 || _history[^1] != line) _history.Add(line);
        if (_history.Count > MaxHistory) _history.RemoveAt(0);

        Add("> " + line, EchoColor);
        _registry.Execute(line, _context);
    }

    // Up/Down walk through earlier lines. Coming back down past the newest restores what was being typed.
    private void BrowseHistory(int step)
    {
        if (_history.Count == 0) return;

        if (_historyIndex == _history.Count) _draft = _field.Text;

        _historyIndex = Math.Clamp(_historyIndex + step, 0, _history.Count);
        _field.Text = _historyIndex == _history.Count ? _draft : _history[_historyIndex];
    }

    // One match: finish the word. Several: finish as far as they agree, and list them if that adds nothing.
    private void TabComplete()
    {
        string text = _field.Text;
        var options = _registry.Complete(text, _context, out int from);
        if (options.Count == 0) return;

        string partial = text.Substring(from);
        string head = text.Substring(0, from);

        if (options.Count == 1)
        {
            _field.Text = head + options[0] + " ";
            return;
        }

        string common = options[0];
        foreach (string o in options)
        {
            int n = 0;
            while (n < common.Length && n < o.Length && char.ToLowerInvariant(common[n]) == char.ToLowerInvariant(o[n])) n++;
            common = common.Substring(0, n);
        }

        if (common.Length > partial.Length) _field.Text = head + common;
        else Add(string.Join("  ", options), EchoColor);
    }

    // =====================  DRAWING  =====================

    public void Draw(SpriteBatch sprites)
    {
        if (_font == null) return;
        if (!IsOpen && !_log.Any(l => l.Age < ShowSeconds)) return;

        float scale = GuiScale.For(_gd);
        float textScale = GuiScale.Text(scale);
        int margin = GuiScale.Pixels(6 * scale);
        int pad = GuiScale.Pixels(4 * scale);
        int lineHeight = GuiScale.Pixels(_font.LineSpacing * textScale);
        int width = Math.Min(_gd.Viewport.Width - 2 * margin, GuiScale.Pixels(640 * scale));
        float Measure(string s) => _font.MeasureString(s).X * textScale;

        // The newest messages that fit, wrapped, oldest at the top
        var rows = new List<(string text, Color color)>();
        int maxRows = IsOpen ? RowsWhenOpen : RowsWhenClosed;
        for (int i = _log.Count - 1; i >= 0 && rows.Count < maxRows; i--)
        {
            var line = _log[i];
            float alpha = IsOpen ? 1f : MathHelper.Clamp((ShowSeconds - line.Age) / FadeSeconds, 0f, 1f);
            if (alpha <= 0f) continue;

            var wrapped = TextWrap.Wrap(line.Text, Measure, width - 2 * pad, MaxRowsPerMessage);
            for (int w = wrapped.Count - 1; w >= 0; w--)
                rows.Add((wrapped[w], line.Color * alpha));
        }
        rows.Reverse();
        if (rows.Count > maxRows) rows.RemoveRange(0, rows.Count - maxRows);

        sprites.Begin(samplerState: SamplerState.PointClamp);

        // Hotbar sits at the bottom centre, so the console lives at the left edge, above the input line
        int inputHeight = lineHeight + 2 * pad;
        int inputY = _gd.Viewport.Height - margin - inputHeight;
        int logBottom = IsOpen ? inputY - pad : _gd.Viewport.Height - margin - GuiScale.Pixels(60 * scale);
        int logTop = logBottom - rows.Count * lineHeight;

        if (IsOpen && rows.Count > 0)
            sprites.Draw(_pixel, new Rectangle(margin, logTop - pad, width, rows.Count * lineHeight + 2 * pad), Color.Black * 0.45f);

        for (int i = 0; i < rows.Count; i++)
        {
            var pos = new Vector2(margin + pad, logTop + i * lineHeight);
            DrawText(sprites, rows[i].text, pos, rows[i].color, textScale);
        }

        if (IsOpen)
        {
            sprites.Draw(_pixel, new Rectangle(margin, inputY, width, inputHeight), Color.Black * 0.7f);

            string shown = "> " + _field.Text;
            bool caretOn = (int)(_time * 2f) % 2 == 0;
            if (caretOn) shown += "_";

            // Long lines scroll: show the end, where the typing is
            while (shown.Length > 1 && Measure(shown) > width - 2 * pad) shown = shown.Substring(1);

            DrawText(sprites, shown, new Vector2(margin + pad, inputY + pad), Color.White, textScale);
        }

        sprites.End();
    }

    private void DrawText(SpriteBatch sprites, string text, Vector2 pos, Color color, float textScale)
    {
        // A dark copy one pixel away keeps the text readable over bright terrain
        sprites.DrawString(_font, text, pos + new Vector2(1, 1), Color.Black * (color.A / 255f), 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
        sprites.DrawString(_font, text, pos, color, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
    }
}
