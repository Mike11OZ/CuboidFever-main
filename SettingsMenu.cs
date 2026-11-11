using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// Multi-page settings screen.
//
// Category page:  Video, Audio, Controls, Resource Packs, Back
// Video page:     Window Mode, Frame Rate Limit, GUI Scale,
//                 Render Distance, Leaf Culling, Ambient Occlusion, Back
// Audio page:     Master Volume, Music Volume, Back
// Packs page:     Default, one button per resource pack found (a few per page), Page, Open Pack Folder, Back
public sealed class SettingsMenu
{
    private readonly GraphicsDevice _graphics;
    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;

    private readonly ButtonMenu _categoryPage;
    private readonly ButtonMenu _videoPage;
    private readonly ButtonMenu _audioPage;
    private readonly ButtonMenu _controlsPage;
    private readonly ButtonMenu _packsPage;
    private ButtonMenu _current;

    private readonly ResourcePackManager _packs;
    private int _packsPageIndex;

    // How many packs fit on one page of the list, so the page never grows taller than the Video page
    private const int PacksPerPage = 4;
    private const int PackNameMaxLength = 26;

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
        ResourcePackManager packs,
        Action cycleRenderDistance,
        Action meshSettingChanged,
        Action<MenuButtonSound> playMenuSound)
    {
        _graphics = graphics;
        _font = font;
        _packs = packs;

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
        _categoryPage.Add("Resource Packs", OpenPacksPage);
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

        // ---------- Resource packs page ----------
        // Its buttons depend on what is in the folder, so they are built each time the page is opened
        // (OpenPacksPage) rather than here.
        _packsPage = new ButtonMenu(graphics, font, playMenuSound)
        {
            DesignHeight = DesignButtonHeight,
            DesignGap = DesignButtonGap,
            DesignWidth = 340
        };

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

    // Looks at the resourcepacks folder again, so a pack dropped in while the game is running is listed
    private void OpenPacksPage()
    {
        _packs.Refresh();
        _packsPageIndex = 0;
        RebuildPacksPage();
        _current = _packsPage;
    }

    // One button per choice: the game's own textures first, then each pack found. The selected one is ticked, and
    // clicking another switches to it straight away. With more than fit on a page, a Page button flips through them.
    private void RebuildPacksPage()
    {
        _packsPage.Clear();

        int entries = _packs.Packs.Count + 1; // entry 0 is the game's own textures
        int pageCount = (entries + PacksPerPage - 1) / PacksPerPage;
        _packsPageIndex = Math.Clamp(_packsPageIndex, 0, pageCount - 1);

        int first = _packsPageIndex * PacksPerPage;
        int end = Math.Min(first + PacksPerPage, entries);

        for (int i = first; i < end; i++)
        {
            if (i == 0)
            {
                _packsPage.Add(
                    () => PackLabel("Default", _packs.ActiveId == null),
                    () => _packs.Select(null));
            }
            else
            {
                ResourcePackInfo pack = _packs.Packs[i - 1];

                _packsPage.Add(
                    () => PackLabel(pack.DisplayName, _packs.IsActive(pack)),
                    () => _packs.Select(pack));
            }
        }

        if (pageCount > 1)
        {
            _packsPage.Add(
                () => $"Page {_packsPageIndex + 1} / {pageCount}",
                () =>
                {
                    _packsPageIndex = (_packsPageIndex + 1) % pageCount;
                    RebuildPacksPage();
                });
        }

        _packsPage.Add("Open Pack Folder", _packs.OpenFolder);
        _packsPage.Add("Back", () => _current = _categoryPage, MenuButtonSound.Back);
    }

    // "[x] Name" for the selected pack, "[ ] Name" for the others. A long name is cut short, and a character
    // the menu font does not have (it covers plain ASCII only) is shown as "?" because drawing one would throw.
    private static string PackLabel(string name, bool selected)
    {
        var text = new System.Text.StringBuilder(name.Length);
        foreach (char c in name)
            text.Append(c >= ' ' && c <= '~' ? c : '?');

        string shown = text.Length <= PackNameMaxLength
            ? text.ToString()
            : text.ToString(0, PackNameMaxLength - 3) + "...";

        return (selected ? "[x] " : "[ ] ") + shown;
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