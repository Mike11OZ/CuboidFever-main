using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// The "Resource Packs" screen: a scrolling list with one row per choice, the game's own textures first and then
// each pack found. A row shows the pack's picture (its pack.png), its name, and whether it is a folder or a zip.
// The pack in use is outlined and tagged "Selected". Mouse wheel and a draggable scrollbar scroll it.
//
// It is laid out and scrolled exactly like WorldListScreen, using the same WorldListLayout, so the two lists look
// and behave alike. Like it, this holds no game logic: clicking a row raises PackSelected and the footer buttons
// raise their own events; SettingsMenu and ResourcePackManager decide what happens. Update needs the mouse cursor
// to be free, as in every menu.
public sealed class ResourcePackListScreen
{
    private sealed class Row
    {
        public ResourcePackInfo Pack; // null for the game's own textures
        public Texture2D Thumbnail;
        public bool ThumbnailTried; // pack.png is read once, the first time the row is on screen
    }

    private readonly GraphicsDevice _graphics;
    private readonly SpriteFont _font; // the large menu font, for the heading; may be null if no font could be loaded
    private readonly SpriteFont _text; // the small menu font, for row text
    private readonly Func<string> _activeId; // the Id of the pack in use, or null for the game's own textures
    private readonly Action<MenuButtonSound> _playSound;
    private readonly Texture2D _pixel;
    private readonly ButtonMenu _openFolderMenu;
    private readonly ButtonMenu _backMenu;
    private readonly RasterizerState _scissor = new() { CullMode = CullMode.None, ScissorTestEnable = true };

    private readonly List<Row> _rows = new();
    private int _scroll;
    private int _hovered = -1;
    private int _pressed = -1;
    private bool _draggingScrollbar;

    public event Action<ResourcePackInfo> PackSelected; // the clicked pack; null means the game's own textures
    public event Action OpenFolderRequested;
    public event Action BackRequested;

    // Same palette as WorldListScreen, so the lists match
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

    // The pack in use: a green outline and tag
    private static readonly Color SelectedColor = new(110, 190, 130);

    private const int DesignBorder = 2;
    private const int DesignMinScrollbarThumb = 24;
    private const float TitleScale = 1f;
    private const string Heading = "Resource Packs";
    private const string SelectedTag = "Selected";

    // The two footer buttons sit side by side, centered as a pair
    private const int DesignFooterButtonHeight = 36;
    private const int DesignOpenFolderWidth = 220;
    private const int DesignBackWidth = 160;
    private const int DesignFooterGap = 12;

    public ResourcePackListScreen(
        GraphicsDevice graphics,
        SpriteFont font,
        SpriteFont smallFont,
        Func<string> activeId,
        Action<MenuButtonSound> playSound)
    {
        _graphics = graphics;
        _font = font;
        _text = smallFont ?? font;
        _activeId = activeId;
        _playSound = playSound;

        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Each button is its own one-button menu, moved left or right of the middle so that the pair is centered.
        // The Back button is the one ButtonMenu makes Escape press.
        int total = DesignOpenFolderWidth + DesignFooterGap + DesignBackWidth;

        _openFolderMenu = new ButtonMenu(graphics, font, playSound)
        {
            DesignWidth = DesignOpenFolderWidth,
            DesignHeight = DesignFooterButtonHeight,
            DesignOffsetX = -total / 2 + DesignOpenFolderWidth / 2
        };
        _openFolderMenu.Add("Open Pack Folder", () => OpenFolderRequested?.Invoke());

        _backMenu = new ButtonMenu(graphics, font, playSound)
        {
            DesignWidth = DesignBackWidth,
            DesignHeight = DesignFooterButtonHeight,
            DesignOffsetX = total / 2 - DesignBackWidth / 2
        };
        _backMenu.Add("Back", () => BackRequested?.Invoke(), MenuButtonSound.Back);
    }

    // Replaces the list: the game's own textures, then the packs in the order given. Scrolls back to the top.
    public void SetPacks(IReadOnlyList<ResourcePackInfo> packs)
    {
        Clear();

        _rows.Add(new Row { ThumbnailTried = true }); // the game's own textures have no picture

        foreach (var pack in packs)
            _rows.Add(new Row { Pack = pack });
    }

    // Empties the list and frees the pictures.
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

    private bool IsActive(Row row) => row.Pack == null ? _activeId() == null : row.Pack.Id == _activeId();

    // =====================  UPDATE  =====================

    public void Update(ActionInput input)
    {
        float scale = GuiScale.For(_graphics);
        ListMetrics m = Metrics();
        Point mouse = input.MousePosition;

        // Keep the footer buttons centered in the footer, wherever the window size has put it
        float buttonTop = m.Bottom + (m.FooterHeight - DesignFooterButtonHeight * scale) / 2f;
        float top = buttonTop / _graphics.Viewport.Height;
        _openFolderMenu.Top = top;
        _backMenu.Top = top;

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
                _playSound?.Invoke(MenuButtonSound.Click);
                PackSelected?.Invoke(_rows[pressed].Pack);
            }
        }

        _openFolderMenu.Update(input);
        _backMenu.Update(input);
    }

    private static int MinThumb(float scale) => GuiScale.Pixels(DesignMinScrollbarThumb * scale);

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

    // The caller (SettingsMenu) has a SpriteBatch open. It is closed and reopened here, because the rows need
    // clipping to the viewport (a scissor rectangle), and that is a setting of Begin.
    public void Draw(SpriteBatch sprites)
    {
        float scale = GuiScale.For(_graphics);
        int screenWidth = _graphics.Viewport.Width;
        ListMetrics m = Metrics();

        DrawHeader(sprites, screenWidth, m);

        var (first, count) = WorldListLayout.VisibleRows(_scroll, _rows.Count, m);

        // Close the caller's batch; every batch opened below is closed again before this method returns
        sprites.End();

        // Pass 1: row backgrounds, text and the scrollbar, nearest-neighbor like the rest of the menu
        BeginClipped(sprites, SamplerState.PointClamp, screenWidth, m);

        for (int i = first; i < first + count; i++)
            DrawRow(sprites, i, m, scale);

        DrawScrollbar(sprites, m);
        sprites.End();

        // Pass 2: pictures, smoothed, because they are usually larger than the box they are shrunk into
        BeginClipped(sprites, SamplerState.LinearClamp, screenWidth, m);

        for (int i = first; i < first + count; i++)
            DrawThumbnail(sprites, i, m, scale);

        sprites.End();

        // Back to the batch the caller expects, with the footer buttons drawn in it
        sprites.Begin(samplerState: SamplerState.PointClamp);
        _openFolderMenu.Draw(sprites);
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

        float scale = GuiScale.For(_graphics);
        float titleScale = TitleScale * GuiScale.Text(scale);
        Vector2 size = _font.MeasureString(Heading) * titleScale;

        var pos = new Vector2(
            MathF.Round((screenWidth - size.X) / 2f),
            MathF.Round((m.HeaderHeight - size.Y) / 2f)
        );

        sprites.DrawString(_font, Heading, pos, TextColor, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
    }

    private Rectangle RowRect(int index, ListMetrics m) =>
        new(m.Left, m.Top + index * m.Stride - _scroll, m.Width, m.RowHeight);

    // The part of a row inside its border, where the picture and text go
    private static Rectangle Inner(Rectangle row, float scale)
    {
        int border = GuiScale.Pixels(DesignBorder * scale);
        return new Rectangle(row.X + border, row.Y + border, row.Width - border * 2, row.Height - border * 2);
    }

    private void DrawRow(SpriteBatch sprites, int index, ListMetrics m, float scale)
    {
        Row row = _rows[index];
        Rectangle rect = RowRect(index, m);
        bool hovered = index == _hovered;
        bool active = IsActive(row);

        sprites.Draw(_pixel, rect, hovered ? BorderHoverColor : active ? SelectedColor : BorderColor);

        Rectangle inner = Inner(rect, scale);
        sprites.Draw(_pixel, inner, hovered ? FillHoverColor : FillColor);

        // Dark box where the picture goes; the picture itself is drawn in the second pass. It stays visible
        // for a pack that has no picture.
        Rectangle picture = PictureRect(inner);
        sprites.Draw(_pixel, picture, PlaceholderColor);

        if (_text == null) return;

        float textScale = GuiScale.Text(scale);
        int textX = picture.Right + GuiScale.Pixels(10 * scale);
        int margin = GuiScale.Pixels(8 * scale);
        int right = inner.Right - margin;

        // The "Selected" tag is on the right of the row, and the text to its left stops short of it
        if (active)
        {
            Vector2 tagSize = _text.MeasureString(SelectedTag) * textScale;
            var tagPos = new Vector2(
                (int)(right - tagSize.X),
                (int)(inner.Y + (inner.Height - tagSize.Y) / 2f));

            sprites.DrawString(_text, SelectedTag, tagPos, SelectedColor, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
            right = (int)tagPos.X - margin;
        }

        int maxWidth = right - textX;
        string name = FitText(row.Pack == null ? "Default" : row.Pack.DisplayName, maxWidth, scale);
        string detail = FitText(Describe(row), maxWidth, scale);

        // The two lines of text are centered vertically in the row
        int blockHeight = GuiScale.Pixels((2 * _text.LineSpacing + 6) * textScale);
        int nameY = inner.Y + (inner.Height - blockHeight) / 2;
        int detailY = nameY + GuiScale.Pixels((_text.LineSpacing + 6) * textScale);

        sprites.DrawString(_text, name, new Vector2(textX, nameY), TextColor, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
        sprites.DrawString(_text, detail, new Vector2(textX, detailY), DimTextColor, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
    }

    private static string Describe(Row row) =>
        row.Pack == null ? "Built-in textures" : row.Pack.IsZip ? "Zip file" : "Folder";

    // The picture box is 16:9 and as tall as the inside of the row, the same as the world list's
    private static Rectangle PictureRect(Rectangle inner) =>
        new(inner.X, inner.Y, WorldListLayout.ThumbnailWidth(inner.Height), inner.Height);

    private void DrawThumbnail(SpriteBatch sprites, int index, ListMetrics m, float scale)
    {
        Row row = _rows[index];
        EnsureThumbnail(row);
        if (row.Thumbnail == null) return;

        // pack.png can be any shape (a square one is the usual), so it is fitted inside the box, not stretched
        Rectangle box = PictureRect(Inner(RowRect(index, m), scale));
        var (x, y, width, height) = WorldListLayout.FitInside(
            box.X, box.Y, box.Width, box.Height, row.Thumbnail.Width, row.Thumbnail.Height);

        sprites.Draw(row.Thumbnail, new Rectangle(x, y, width, height), Color.White);
    }

    // Reads the pack's pack.png the first time its row is drawn. A missing or unreadable picture leaves the
    // dark placeholder in place and is not tried again.
    private void EnsureThumbnail(Row row)
    {
        if (row.ThumbnailTried) return;
        row.ThumbnailTried = true;

        byte[] png = row.Pack?.TryReadThumbnail();
        if (png == null) return;

        try
        {
            using var stream = new MemoryStream(png);
            row.Thumbnail = Texture2D.FromStream(_graphics, stream);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Warning] Could not load the picture of resource pack '{row.Pack.Id}': {e.Message}");
        }
    }

    private void DrawScrollbar(SpriteBatch sprites, ListMetrics m)
    {
        float scale = GuiScale.For(_graphics);
        var thumb = WorldListLayout.Thumb(_rows.Count, _scroll, m, MinThumb(scale));
        if (thumb == null) return; // everything fits, nothing to scroll

        sprites.Draw(_pixel, new Rectangle(m.ScrollbarLeft, m.Top, m.ScrollbarWidth, m.Height), TrackColor);

        var handle = new Rectangle(m.ScrollbarLeft, m.Top + thumb.Value.offset, m.ScrollbarWidth, thumb.Value.length);
        bool active = _draggingScrollbar;
        sprites.Draw(_pixel, handle, active ? ThumbHoverColor : ThumbColor);
    }

    // =====================  TEXT  =====================

    // Text that fits in maxWidth pixels at the given scale: characters the font lacks become '?' (a SpriteFont
    // throws on them, and a pack's name is the player's own file name), and a too-long end is replaced by "...".
    private string FitText(string text, int maxWidth, float scale)
    {
        var fitted = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
            fitted.Append(_text.Characters.Contains(c) ? c : '?');

        string result = fitted.ToString();
        if (_text.MeasureString(result).X * GuiScale.Text(scale) <= maxWidth) return result;

        while (result.Length > 1)
        {
            result = result.Substring(0, result.Length - 1);
            string candidate = result.TrimEnd() + "...";

            if (_text.MeasureString(candidate).X * GuiScale.Text(scale) <= maxWidth) return candidate;
        }

        return result;
    }
}
