using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// Draws a NoticeModel as a dark red message box across the top of the screen: a few lines of small text that
// fade out by themselves. Used by the menus and by the game itself, so both show messages the same way.
public sealed class NoticeBanner
{
    private readonly GraphicsDevice _graphics;
    private readonly SpriteFont _font; // may be null if no font could be loaded
    private readonly Texture2D _pixel;
    private readonly NoticeModel _notice = new();

    public NoticeBanner(GraphicsDevice graphics, SpriteFont font)
    {
        _graphics = graphics;
        _font = font;

        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    // Shows a message for the given number of seconds (a few, if not said), replacing any message showing.
    public void Show(string message, float seconds = NoticeModel.DefaultSeconds) => _notice.Show(message, seconds);

    public void Update(float deltaTime) => _notice.Update(deltaTime);

    // The caller has a SpriteBatch open.
    public void Draw(SpriteBatch sprites)
    {
        if (!_notice.IsActive || _font == null) return;

        float scale = GuiScale.For(_graphics);
        int screenWidth = _graphics.Viewport.Width;
        float fade = _notice.Opacity;

        int padding = GuiScale.Pixels(10 * scale);
        int width = System.Math.Min(GuiScale.Pixels(520 * scale), screenWidth - 2 * WorldListLayout.ScreenMargin);
        var lines = TextWrap.Wrap(_notice.Text, s => _font.MeasureString(s).X * GuiScale.Text(scale), width - 2 * padding, 3);
        if (lines.Count == 0) return;

        float textScale = GuiScale.Text(scale);
        int lineHeight = GuiScale.Pixels(_font.LineSpacing * textScale);
        int height = lines.Count * lineHeight + 2 * padding;
        var box = new Rectangle((screenWidth - width) / 2, GuiScale.Pixels(8 * scale), width, height);

        sprites.Draw(_pixel, box, new Color(140, 70, 82) * fade);
        int border = GuiScale.Pixels(2 * scale);
        sprites.Draw(_pixel,
            new Rectangle(box.X + border, box.Y + border, box.Width - 2 * border, box.Height - 2 * border),
            new Color(60, 26, 36) * fade);

        for (int i = 0; i < lines.Count; i++)
        {
            sprites.DrawString(_font, lines[i], new Vector2(box.X + padding, box.Y + padding + i * lineHeight),
                new Color(245, 235, 238) * fade, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
        }
    }
}
