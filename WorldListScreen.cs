using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// One saved world as the list shows it.
public sealed record WorldListEntry(string FolderPath, string Name, DateTime LastPlayedUtc);

// The "Select World" screen: a scrolling list with one row per saved world, each with its thumbnail, name and
// when it was last played. Mouse wheel and a draggable scrollbar scroll it, so any number of worlds fit.
//
// Like ButtonMenu it holds no game logic: picking a row raises WorldSelected with the world's folder and the
// Back button raises BackRequested; MainMenu and Game1 decide what happens. All sizes come from WorldListLayout,
// where they can be tested. Update needs the mouse cursor to be free, as in every menu.
public sealed class WorldListScreen
{
    private sealed class Row
    {
        public WorldListEntry Entry;
        public Texture2D Thumbnail;
        public bool ThumbnailTried; // the file is read once, the first time the row is on screen
    }

    private readonly GraphicsDevice _graphics;
    private readonly SpriteFont _font; // may be null if no font could be loaded
    private readonly Action<MenuButtonSound> _playSound;
    private readonly Texture2D _pixel;
    private readonly ButtonMenu _backMenu;
    private readonly RasterizerState _scissor = new() { CullMode = CullMode.None, ScissorTestEnable = true };

    private readonly List<Row> _rows = new();
    private int _scroll;
    private int _hovered = -1;
    private int _pressed = -1;
    private bool _draggingScrollbar;

    public event Action<string> WorldSelected;  // the folder of the picked world
    public event Action BackRequested;

    // Same palette as ButtonMenu, so the list matches the other menus
    private static readonly Color FillColor = new(44, 40, 62);
    private static readonly Color FillHoverColor = new(72, 66, 102);
    private static readonly Color BorderColor = new(90, 84, 122);
    private static readonly Color BorderHoverColor = new(235, 235, 245);
    private static readonly Color TextColor = new(235, 235, 245);
    private static readonly Color DimTextColor = new(150, 144, 180);
    private static readonly Color PlaceholderColor = new(24, 22, 34);
    private static readonly Color TrackColor = new(30, 27, 44);
    private static readonly Color ThumbColor = new(90, 84, 122);
    private static readonly Color ThumbHoverColor = new(150, 144, 190);

    private const int DesignBorder = 2;
    private const int DesignMinScrollbarThumb = 24;
    private const float TitleScale = 2f;

    public WorldListScreen(GraphicsDevice graphics, SpriteFont font, Action<MenuButtonSound> playSound)
    {
        _graphics = graphics;
        _font = font;
        _playSound = playSound;

        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // The Back button lives in the footer. ButtonMenu also makes Escape press it.
        _backMenu = new ButtonMenu(graphics, font, playSound) { DesignWidth = 200, DesignHeight = 36 };
        _backMenu.Add("Back", () => BackRequested?.Invoke(), MenuButtonSound.Back);
    }

    // Replaces the list (most recent first, as given) and scrolls back to the top.
    public void SetWorlds(IReadOnlyList<WorldListEntry> worlds)
    {
        Clear();

        foreach (var entry in worlds)
            _rows.Add(new Row { Entry = entry });
    }

    // Empties the list and frees the thumbnail pictures.
    public void Clear()
    {
        foreach (var row in _rows)
            row.Thumbnail?.Dispose();

        _rows.Clear();
        _scroll = 0;
        _hovered = -1;
        _pressed = -1;
        _draggingScrollbar = false;
    }

    private ListMetrics Metrics() =>
        WorldListLayout.Metrics(_graphics.Viewport.Width, _graphics.Viewport.Height, GuiScale.For(_graphics));

    // =====================  UPDATE  =====================

    public void Update(ActionInput input)
    {
        int scale = GuiScale.For(_graphics);
        ListMetrics m = Metrics();
        Point mouse = input.MousePosition;

        // Keep the Back button centered in the footer, wherever the window size has put it
        float buttonTop = m.Bottom + (m.FooterHeight - _backMenu.DesignHeight * scale) / 2f;
        _backMenu.Top = buttonTop / _graphics.Viewport.Height;

        // Mouse wheel: one row per notch (a notch is 120)
        if (input.ScrollDelta != 0)
            _scroll -= input.ScrollDelta * m.Stride / 120;

        // Scrollbar: grab it anywhere along the track, and the thumb follows the mouse until the button is released
        if (input.WasLeftClicked && InScrollbar(mouse, m) && WorldListLayout.Thumb(_rows.Count, _scroll, m, MinThumb(scale)) != null)
            _draggingScrollbar = true;

        if (input.WasLeftReleased)
            _draggingScrollbar = false;

        if (_draggingScrollbar)
            _scroll = WorldListLayout.ScrollForThumbCenter(mouse.Y, _rows.Count, m, MinThumb(scale));

        _scroll = WorldListLayout.ClampScroll(_scroll, _rows.Count, m);

        // Rows: a row fires when the mouse is released over the row it was pressed on, like ButtonMenu
        _hovered = _draggingScrollbar ? -1 : WorldListLayout.RowAt(mouse.X, mouse.Y, _scroll, _rows.Count, m);

        if (input.WasLeftClicked && !_draggingScrollbar)
            _pressed = _hovered;

        if (input.WasLeftReleased)
        {
            int pressed = _pressed;
            _pressed = -1;

            if (pressed >= 0 && pressed == _hovered)
            {
                _playSound?.Invoke(MenuButtonSound.Start);
                WorldSelected?.Invoke(_rows[pressed].Entry.FolderPath);
                return; // the click may switch screens, so nothing else runs after it
            }
        }

        _backMenu.Update(input);
    }

    private static int MinThumb(int scale) => DesignMinScrollbarThumb * scale;

    // The scrollbar's column, a little wider than it is drawn so it is easy to grab
    private static bool InScrollbar(Point mouse, ListMetrics m)
    {
        int slack = m.ScrollbarGap / 2;

        return mouse.X >= m.ScrollbarLeft - slack
            && mouse.X < m.ScrollbarLeft + m.ScrollbarWidth + slack
            && mouse.Y >= m.Top
            && mouse.Y < m.Bottom;
    }

    // =====================  DRAW  =====================

    // The caller (MainMenu) has a SpriteBatch open. It is closed and reopened here, because the rows need
    // clipping to the viewport (a scissor rectangle), and that is a setting of Begin.
    public void Draw(SpriteBatch sprites)
    {
        int scale = GuiScale.For(_graphics);
        int screenWidth = _graphics.Viewport.Width;
        ListMetrics m = Metrics();

        DrawHeader(sprites, screenWidth, m);

        if (_rows.Count == 0)
            DrawCentered(sprites, "No saved worlds yet", screenWidth, m.Top + m.Height / 2, scale, DimTextColor);

        var (first, count) = WorldListLayout.VisibleRows(_scroll, _rows.Count, m);

        // Close the caller's batch; every batch opened below is closed again before this method returns
        sprites.End();

        // Pass 1: row backgrounds, text and the scrollbar, nearest-neighbor like the rest of the menu
        BeginClipped(sprites, SamplerState.PointClamp, screenWidth, m);

        for (int i = first; i < first + count; i++)
            DrawRow(sprites, i, m, scale);

        DrawScrollbar(sprites, m, scale);
        sprites.End();

        // Pass 2: thumbnails, smoothed, because they are screenshots shrunk further to fit the row
        BeginClipped(sprites, SamplerState.LinearClamp, screenWidth, m);

        for (int i = first; i < first + count; i++)
            DrawThumbnail(sprites, i, m, scale);

        sprites.End();

        // Back to the batch the caller expects, with the Back button drawn in it
        sprites.Begin(samplerState: SamplerState.PointClamp);
        _backMenu.Draw(sprites);
    }

    // Opens a batch that only draws inside the viewport. The caller closes it with End.
    private void BeginClipped(SpriteBatch sprites, SamplerState sampler, int screenWidth, ListMetrics m)
    {
        // The scissor rectangle has to stay inside the screen, however small the window gets
        int top = Math.Clamp(m.Top, 0, _graphics.Viewport.Height - 1);
        int bottom = Math.Clamp(m.Bottom, top + 1, _graphics.Viewport.Height);
        _graphics.ScissorRectangle = new Rectangle(0, top, screenWidth, bottom - top);

        sprites.Begin(samplerState: sampler, rasterizerState: _scissor);
    }

    private void DrawHeader(SpriteBatch sprites, int screenWidth, ListMetrics m)
    {
        if (_font == null) return;

        int scale = GuiScale.For(_graphics);
        float titleScale = TitleScale * scale;
        Vector2 size = _font.MeasureString("Select World") * titleScale;

        var pos = new Vector2(
            MathF.Round((screenWidth - size.X) / 2f),
            MathF.Round((m.HeaderHeight - size.Y) / 2f)
        );

        sprites.DrawString(_font, "Select World", pos, TextColor, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
    }

    private void DrawCentered(SpriteBatch sprites, string text, int screenWidth, int centerY, int scale, Color color)
    {
        if (_font == null) return;

        Vector2 size = _font.MeasureString(text) * scale;
        var pos = new Vector2(MathF.Round((screenWidth - size.X) / 2f), MathF.Round(centerY - size.Y / 2f));

        sprites.DrawString(_font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private Rectangle RowRect(int index, ListMetrics m) =>
        new(m.Left, m.Top + index * m.Stride - _scroll, m.Width, m.RowHeight);

    // The part of a row inside its border, where the picture and text go
    private static Rectangle Inner(Rectangle row, int scale)
    {
        int border = DesignBorder * scale;
        return new Rectangle(row.X + border, row.Y + border, row.Width - border * 2, row.Height - border * 2);
    }

    private void DrawRow(SpriteBatch sprites, int index, ListMetrics m, int scale)
    {
        Rectangle rect = RowRect(index, m);
        bool hovered = index == _hovered;

        sprites.Draw(_pixel, rect, hovered ? BorderHoverColor : BorderColor);

        Rectangle inner = Inner(rect, scale);
        sprites.Draw(_pixel, inner, hovered ? FillHoverColor : FillColor);

        // Dark box where the picture goes; the picture itself is drawn in the second pass. It stays visible
        // for a world that has no thumbnail yet.
        Rectangle picture = PictureRect(inner);
        sprites.Draw(_pixel, picture, PlaceholderColor);

        if (_font == null) return;

        WorldListEntry entry = _rows[index].Entry;

        int textX = picture.Right + 10 * scale;
        int maxWidth = inner.Right - textX - 8 * scale;

        string name = FitText(WorldListLayout.DisplayName(entry.Name), maxWidth, scale);
        string played = FitText("Last played: " + FormatLastPlayed(entry.LastPlayedUtc), maxWidth, scale);

        int nameY = inner.Y + 8 * scale;
        int playedY = nameY + (_font.LineSpacing + 6) * scale;

        sprites.DrawString(_font, name, new Vector2(textX, nameY), TextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        sprites.DrawString(_font, played, new Vector2(textX, playedY), DimTextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    // The thumbnail is 16:9 and as tall as the inside of the row
    private static Rectangle PictureRect(Rectangle inner) =>
        new(inner.X, inner.Y, WorldListLayout.ThumbnailWidth(inner.Height), inner.Height);

    private void DrawThumbnail(SpriteBatch sprites, int index, ListMetrics m, int scale)
    {
        Row row = _rows[index];
        EnsureThumbnail(row);
        if (row.Thumbnail == null) return;

        Rectangle picture = PictureRect(Inner(RowRect(index, m), scale));
        sprites.Draw(row.Thumbnail, picture, Color.White);
    }

    // Reads the world's thumbnail the first time its row is drawn. A missing or unreadable picture leaves the
    // dark placeholder in place and is not tried again.
    private void EnsureThumbnail(Row row)
    {
        if (row.ThumbnailTried) return;
        row.ThumbnailTried = true;

        byte[] png = WorldThumbnail.TryRead(row.Entry.FolderPath);
        if (png == null) return;

        try
        {
            using var stream = new MemoryStream(png);
            row.Thumbnail = Texture2D.FromStream(_graphics, stream);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[Save] Could not load the thumbnail in '{row.Entry.FolderPath}': {e.Message}");
        }
    }

    private void DrawScrollbar(SpriteBatch sprites, ListMetrics m, int scale)
    {
        var thumb = WorldListLayout.Thumb(_rows.Count, _scroll, m, MinThumb(scale));
        if (thumb == null) return; // everything fits, nothing to scroll

        sprites.Draw(_pixel, new Rectangle(m.ScrollbarLeft, m.Top, m.ScrollbarWidth, m.Height), TrackColor);

        var handle = new Rectangle(m.ScrollbarLeft, m.Top + thumb.Value.offset, m.ScrollbarWidth, thumb.Value.length);
        bool active = _draggingScrollbar;
        sprites.Draw(_pixel, handle, active ? ThumbHoverColor : ThumbColor);
    }

    // =====================  TEXT  =====================

    // Text that fits in maxWidth pixels at the given scale: characters the font lacks become '?' (a SpriteFont
    // throws on them, and a world name is the player's own text), and a too-long end is replaced by "...".
    private string FitText(string text, int maxWidth, int scale)
    {
        var fitted = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
            fitted.Append(_font.Characters.Contains(c) ? c : '?');

        string result = fitted.ToString();
        if (_font.MeasureString(result).X * scale <= maxWidth) return result;

        while (result.Length > 1)
        {
            result = result.Substring(0, result.Length - 1);
            string candidate = result.TrimEnd() + "...";

            if (_font.MeasureString(candidate).X * scale <= maxWidth) return candidate;
        }

        return result;
    }

    // Local time, always the same shape, whatever the computer's language settings are
    private static string FormatLastPlayed(DateTime lastPlayedUtc) =>
        lastPlayedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
