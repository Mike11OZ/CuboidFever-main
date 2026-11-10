using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// Multi-page settings screen.
//
// Category page:  Video, Audio, Controls, Back
// Video page:     Window Mode, Frame Rate Limit, GUI Scale,
//                 Render Distance, Leaf Culling, Ambient Occlusion, Back
// Audio page:     Master Volume, Music Volume, Back
public sealed class SettingsMenu
{
    private readonly GraphicsDevice _graphics;
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;

    private readonly ButtonMenu _categoryPage;
    private readonly ButtonMenu _videoPage;
    private readonly ButtonMenu _audioPage;
    private readonly ButtonMenu _controlsPage;
    private ButtonMenu _current;

    private const string Title = "Settings";
    private const float TitleScale = 2f;
    private const int DesignMargin = 12;
    private const int DesignButtonHeight = 40;
    private const int DesignButtonGap = 12;
    private const float DimAmount = 0.6f;

    private float _titleY;

    public event Action BackRequested;

    public SettingsMenu(
        GraphicsDevice graphics,
        SpriteFont font,
        FrameRateLimiter fps,
        WindowManager window,
        AudioManager audio,
        Action cycleRenderDistance,
        Action meshSettingChanged,
        Action<MenuButtonSound> playMenuSound)
    {
        _graphics = graphics;
        _font = font;

        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // ---------- Category page ----------
        _categoryPage = new ButtonMenu(graphics, font, playMenuSound)
        {
            DesignHeight = DesignButtonHeight,
            DesignGap = DesignButtonGap
        };

        _categoryPage.Add("Video", () => _current = _videoPage);
        _categoryPage.Add("Audio", () => _current = _audioPage);
        _categoryPage.Add("Controls", () => _current = _controlsPage);
        _categoryPage.Add("Back", () => BackRequested?.Invoke(), MenuButtonSound.Back);

        // ---------- Video page ----------
        _videoPage = new ButtonMenu(graphics, font, playMenuSound)
        {
            DesignHeight = DesignButtonHeight,
            DesignGap = DesignButtonGap
        };

        _videoPage.Add(
            () => $"Window Mode: {window.CurrentMode}",
            window.CycleNextMode
        );

        _videoPage.Add(
            () => $"Frame Rate Limit: {(fps.CurrentCap == 0 ? "Unlimited" : fps.CurrentCap.ToString())}",
            fps.CycleNext
        );

        _videoPage.Add(
            () => $"GUI Scale: {GuiScale.Label(graphics)}",
            () => GuiScale.CycleSetting(graphics)
        );

        _videoPage.Add(
            () => $"Render Distance: {World.RenderDistanceSetting} chunks",
            cycleRenderDistance
        );

        _videoPage.Add(
            () => $"Leaf Culling: {LeafCullLabel(ChunkMesher.LeafCullDepth)}",
            () =>
            {
                ChunkMesher.LeafCullDepth = (ChunkMesher.LeafCullDepth + 1) % (ChunkMesher.MaxLeafDepth + 1);
                meshSettingChanged();
            }
        );

        _videoPage.Add(
            () => $"Ambient Occlusion: {(ChunkMesher.AmbientOcclusion ? "On" : "Off")}",
            () =>
            {
                ChunkMesher.AmbientOcclusion = !ChunkMesher.AmbientOcclusion;
                meshSettingChanged();
            }
        );

        _videoPage.Add("Back", () => _current = _categoryPage, MenuButtonSound.Back);

        // ---------- Audio page ----------
        _audioPage = new ButtonMenu(graphics, font, playMenuSound)
        {
            DesignHeight = DesignButtonHeight,
            DesignGap = DesignButtonGap
        };

        _audioPage.Add(
            () => $"Master Volume: {MathF.Round(audio.MasterVolume * 100)}%",
            () => audio.MasterVolume = CycleVolume(audio.MasterVolume)
        );

        _audioPage.Add(
            () => $"Music Volume: {MathF.Round(audio.MusicVolume * 100)}%",
            () => audio.MusicVolume = CycleVolume(audio.MusicVolume)
        );

        _audioPage.Add("Back", () => _current = _categoryPage, MenuButtonSound.Back);

        // ---------- Controls page ----------
        _controlsPage = CreatePlaceholderPage(graphics, font, playMenuSound, "Controls settings coming soon");

        // Set design widths for long labels
        if (font != null)
        {
            string[] longestVideo =
            {
                "Window Mode: Borderless",
                "Frame Rate Limit: Unlimited",
                "GUI Scale: Auto (4)",
                "Render Distance: 32 chunks",
                "Leaf Culling: Depth 3",
                "Ambient Occlusion: Off",
            };

            _videoPage.DesignWidth = (int)longestVideo.Max(s => font.MeasureString(s).X) + 60;

            string[] longestAudio =
            {
                "Master Volume: 100%",
                "Music Volume: 100%"
            };

            _audioPage.DesignWidth = (int)longestAudio.Max(s => font.MeasureString(s).X) + 60;
        }

        _current = _categoryPage;
    }

    private ButtonMenu CreatePlaceholderPage(
        GraphicsDevice graphics,
        SpriteFont font,
        Action<MenuButtonSound> playMenuSound,
        string message)
    {
        var page = new ButtonMenu(graphics, font, playMenuSound)
        {
            DesignHeight = DesignButtonHeight,
            DesignGap = DesignButtonGap
        };

        page.Add(message, () => { });
        page.Add("Back", () => _current = _categoryPage, MenuButtonSound.Back);
        return page;
    }

    private static string LeafCullLabel(int depth) =>
        depth <= 0 ? "Off" : $"Depth {depth}";

    private static float CycleVolume(float currentVolume)
    {
        float next = MathF.Round(currentVolume + 0.1f, 1);
        return next > 1.0f ? 0.0f : next;
    }

    private void UpdateLayout()
    {
        float scale = GuiScale.For(_graphics);
        int screenHeight = _graphics.Viewport.Height;

        const float TitleTopFraction = 0.17f;
        _titleY = screenHeight * TitleTopFraction;

        float titleHeight = _font != null ? _font.LineSpacing * TitleScale * GuiScale.Text(scale) : 0f;
        float margin = DesignMargin * scale;

        float buttonsTop = _titleY + titleHeight + margin;
        _current.Top = buttonsTop / screenHeight;
    }

    public void Update(ActionInput input)
    {
        UpdateLayout();
        _current.Update(input);
    }

    public void ShowCategoryPage() => _current = _categoryPage;

    public void Draw(SpriteBatch sprites, GraphicsDevice graphics, bool overWorld)
    {
        UpdateLayout();

        if (!overWorld)
            graphics.Clear(LoadingScreen.BackgroundColor);

        int screenWidth = graphics.Viewport.Width;
        int screenHeight = graphics.Viewport.Height;

        sprites.Begin(samplerState: SamplerState.PointClamp);

        if (overWorld)
        {
            sprites.Draw(
                _pixel,
                new Rectangle(0, 0, screenWidth, screenHeight),
                Color.Black * DimAmount
            );
        }

        if (_font != null)
        {
            float titleScale = TitleScale * GuiScale.Text(GuiScale.For(graphics));
            Vector2 titleSize = _font.MeasureString(Title) * titleScale;

            var titlePos = new Vector2(
                MathF.Round((screenWidth - titleSize.X) / 2f),
                MathF.Round(_titleY)
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

        _current.Draw(sprites);

        sprites.End();
    }
}