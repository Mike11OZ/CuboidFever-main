using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// The title screen: the game's name and a small tree of button pages.
//
//     Main:  Play Game, Settings, Exit Game
//     Play:  Play Solo, Play Multiplayer, Back
//     Solo:  Load World, New World, Back
//     Worlds (from Load World): one button per saved world, four to a page, Next Page, Back
//
// Moving between pages is only navigation, so MainMenu handles it itself. Everything that actually does
// something raises an event and Game1 decides what happens (start a world, open settings, quit).
public sealed class MainMenu
{
    private readonly SpriteFont _font;
    private readonly PanoramaBackground _panorama;

    // One button stack per page. _current is the page being shown and updated.
    private readonly ButtonMenu _mainPage;
    private readonly ButtonMenu _playPage;
    private readonly ButtonMenu _soloPage;
    private readonly ButtonMenu _worldsPage;
    private ButtonMenu _current;

    // The saved worlds the list is showing (most recent first), and which page of them
    private List<(string FolderPath, string Name)> _worlds = new();
    private int _worldsPageIndex;
    private const int MaxWorldNameLength = 26;

    private const float TitleScale = 2f;
    private const float TitleTopFraction = 0.18f;

    public event Action LoadWorldRequested;  // the "Load World" button: Game1 answers with ShowWorldList
    public event Action<string> WorldSelected; // a world was picked from the list (its folder)
    public event Action NewWorldRequested;   // create a fresh world
    public event Action SettingsRequested;   // the "Settings" button
    public event Action ExitRequested;

    public MainMenu(
        GraphicsDevice graphics,
        SpriteFont font,
        Action<MenuButtonSound> playMenuSound)
    {
        _font = font;

        _panorama = new PanoramaBackground(
            graphics,
            "cuboidfever/textures/gui/menu_panorama.png"
        );

        // Every page passes the audio callback to its own button menu. Each has three buttons, so they all
        // use the default layout.
        _mainPage = new ButtonMenu(graphics, font, playMenuSound);
        _playPage = new ButtonMenu(graphics, font, playMenuSound);
        _soloPage = new ButtonMenu(graphics, font, playMenuSound);

        // The world list has more buttons than the other pages (up to four worlds, Next Page and Back), so its
        // buttons are shorter and closer together to keep it on screen in a small window. Wider, for names.
        _worldsPage = new ButtonMenu(graphics, font, playMenuSound)
        {
            Top = 0.32f,
            DesignWidth = 340,
            DesignHeight = 36,
            DesignGap = 8
        };

        // ---- Main page ----
        _mainPage.Add("Play Game", () => _current = _playPage);
        _mainPage.Add("Settings", () => SettingsRequested?.Invoke());
        _mainPage.Add("Exit Game", () => ExitRequested?.Invoke());

        // ---- Play page ----
        _playPage.Add("Singleplayer", () => _current = _soloPage);

        // Placeholder until multiplayer exists: it clicks like a button but goes nowhere.
        _playPage.Add("Multiplayer", () => { });

        _playPage.Add("Back", () => _current = _mainPage, MenuButtonSound.Back);

        // ---- Solo page ----
        // Load World only opens the list of saved worlds, so it is an ordinary click. Picking a world from the
        // list, and New World, start a world and use the "Start" sound.
        _soloPage.Add("Load World", () => LoadWorldRequested?.Invoke());
        _soloPage.Add(
            "New World",
            () => NewWorldRequested?.Invoke(),
            MenuButtonSound.Start
        );
        _soloPage.Add("Back", () => _current = _playPage, MenuButtonSound.Back);

        _current = _mainPage;
    }

    // Goes back to the first page. Called when the title screen is entered, so leaving a world always lands
    // on "Play Game / Settings / Exit Game" instead of a sub-menu left open earlier.
    public void ShowMainPage() => _current = _mainPage;

    // Opens the list of saved worlds, in the order given (most recent first). Game1 calls this when Load World is
    // clicked, because only it knows what is on disk.
    public void ShowWorldList(IReadOnlyList<(string FolderPath, string Name)> worlds)
    {
        _worlds = new List<(string FolderPath, string Name)>(worlds);
        _worldsPageIndex = 0;

        BuildWorldsPage();
        _current = _worldsPage;
    }

    private void BuildWorldsPage()
    {
        _worldsPage.Clear();

        int pageCount = WorldListLayout.PageCount(_worlds.Count);
        var (start, count) = WorldListLayout.PageRange(_worlds.Count, _worldsPageIndex);

        // Clicks like a button but goes nowhere, like the Multiplayer placeholder
        if (_worlds.Count == 0)
            _worldsPage.Add("No saved worlds yet", () => { });

        for (int i = start; i < start + count; i++)
        {
            string folder = _worlds[i].FolderPath;
            string label = FitToFont(WorldListLayout.Shorten(_worlds[i].Name, MaxWorldNameLength));

            _worldsPage.Add(label, () => WorldSelected?.Invoke(folder), MenuButtonSound.Start);
        }

        if (pageCount > 1)
        {
            _worldsPage.Add($"Next Page ({_worldsPageIndex + 1}/{pageCount})", () =>
            {
                _worldsPageIndex = (_worldsPageIndex + 1) % pageCount;
                BuildWorldsPage();
            });
        }

        _worldsPage.Add("Back", () => _current = _soloPage, MenuButtonSound.Back);
    }

    // A SpriteFont throws on a character it was not built with, and a world name is the player's own text. Swap any
    // character the font lacks for '?', so an unusual name can never crash the menu.
    private string FitToFont(string text)
    {
        if (_font == null) return text;

        var fitted = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
            fitted.Append(_font.Characters.Contains(c) ? c : '?');

        return fitted.ToString();
    }

    public void Update(ActionInput input, float deltaTime)
    {
        _panorama.Update(deltaTime);
        _current.Update(input);
    }

    public void Draw(SpriteBatch sprites, GraphicsDevice graphics)
    {
        int screenWidth = graphics.Viewport.Width;
        int screenHeight = graphics.Viewport.Height;

        // 1. Scrolling panorama (handles its own SpriteBatch)
        _panorama.Draw(sprites, graphics);

        // 2. UI on top
        sprites.Begin(samplerState: SamplerState.PointClamp);

        if (_font != null)
        {
            // Everything is drawn at the GUI scale, so the title and version text grow with the buttons
            int scale = GuiScale.For(graphics);
            float titleScale = TitleScale * scale;

            Vector2 titleSize = _font.MeasureString(GameInfo.Title);

            var titlePos = new Vector2(
                MathF.Round((screenWidth - titleSize.X * titleScale) / 2f),
                MathF.Round(screenHeight * TitleTopFraction)
            );

            sprites.DrawString(
                _font,
                GameInfo.Title,
                titlePos,
                Color.White,
                0f,
                Vector2.Zero,
                titleScale,
                SpriteEffects.None,
                0f
            );

            var versionPos = new Vector2(
                12 * scale,
                screenHeight - (_font.LineSpacing + 8) * scale
            );

            sprites.DrawString(
                _font,
                $"{GameInfo.Version} ({GameInfo.BuildType})",
                versionPos,
                new Color(90, 84, 122),
                0f,
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                0f
            );
        }

        _current.Draw(sprites);

        sprites.End();
    }
}