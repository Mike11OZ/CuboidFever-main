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
//     New World (from New World): name and seed boxes, Create World, Cancel (NewWorldScreen)
//     Worlds (from Load World): a scrolling list of saved worlds with thumbnails (WorldListScreen), Back
//
// Moving between pages is only navigation, so MainMenu handles it itself. Everything that actually does
// something raises an event and Game1 decides what happens (start a world, open settings, quit).
public sealed class MainMenu
{
    private readonly SpriteFont _font;
    private readonly PanoramaBackground _panorama;

    // A message that fades away by itself ("Could not open 'World 3': ..."), shown over whatever screen is open
    private readonly NoticeBanner _notice;

    // A question over the whole menu ("Open this world anyway?"). While it is open nothing else responds.
    private readonly ConfirmDialog _confirm;

    // One button stack per page. _current is the page being shown and updated.
    private readonly ButtonMenu _mainPage;
    private readonly ButtonMenu _playPage;
    private readonly ButtonMenu _soloPage;
    private ButtonMenu _current;

    // The list of saved worlds is not a stack of buttons, so it is its own screen. While it is showing, it is
    // updated and drawn instead of _current.
    private readonly WorldListScreen _worldList;
    private bool _showingWorldList;

    // Same for the New World screen, with its text boxes
    private readonly NewWorldScreen _newWorld;
    private bool _showingNewWorld;

    private const float TitleScale = 2f;
    private const float TitleTopFraction = 0.18f;

    public event Action LoadWorldRequested;  // the "Load World" button: Game1 answers with ShowWorldList
    public event Action<string> WorldSelected; // a world was picked from the list (its folder)
    public event Action<string> DeleteWorldRequested; // the player confirmed deleting a world (its folder)
    public event Action NewWorldRequested;   // the "New World" button: Game1 answers with ShowNewWorld
    public event Action<string, string> CreateWorldRequested; // Create World was pressed (name and seed, as typed)
    public event Action SettingsRequested;   // the "Settings" button
    public event Action ExitRequested;

    public MainMenu(
        GraphicsDevice graphics,
        SpriteFont font,
        SpriteFont smallFont,
        Action<MenuButtonSound> playMenuSound)
    {
        _font = font;
        _notice = new NoticeBanner(graphics, smallFont ?? font);
        _confirm = new ConfirmDialog(graphics, font, smallFont, playMenuSound);

        _panorama = new PanoramaBackground(
            graphics,
            "cuboidfever/textures/gui/menu_panorama.png"
        );

        // Every page passes the audio callback to its own button menu. Each has three buttons, so they all
        // use the default layout.
        _mainPage = new ButtonMenu(graphics, font, playMenuSound);
        _playPage = new ButtonMenu(graphics, font, playMenuSound);
        _soloPage = new ButtonMenu(graphics, font, playMenuSound);

        _worldList = new WorldListScreen(graphics, font, smallFont, playMenuSound);
        _worldList.WorldSelected += folder => WorldSelected?.Invoke(folder);
        _worldList.DeleteRequested += folder => DeleteWorldRequested?.Invoke(folder);
        _worldList.BackRequested += () =>
        {
            _showingWorldList = false;
            _worldList.Clear();
            _current = _soloPage;
        };

        _newWorld = new NewWorldScreen(graphics, font, smallFont, playMenuSound);
        _newWorld.CreateRequested += (name, seed) => CreateWorldRequested?.Invoke(name, seed);
        _newWorld.BackRequested += () =>
        {
            _showingNewWorld = false;
            _current = _soloPage;
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
        // Load World and New World only open another screen, so they are ordinary clicks. Picking a world from
        // the list, and Create World, start a world and use the "Start" sound.
        _soloPage.Add("Load World", () => LoadWorldRequested?.Invoke());
        _soloPage.Add("New World", () => NewWorldRequested?.Invoke());
        _soloPage.Add("Back", () => _current = _playPage, MenuButtonSound.Back);

        _current = _mainPage;
    }

    // Goes back to the first page. Called when the title screen is entered, so leaving a world always lands
    // on "Play Game / Settings / Exit Game" instead of a sub-menu left open earlier.
    public void ShowMainPage()
    {
        _showingWorldList = false;
        _showingNewWorld = false;
        _worldList.Clear(); // frees the thumbnails
        _current = _mainPage;
    }

    // Opens the list of saved worlds, in the order given (most recent first). Game1 calls this when Load World is
    // clicked, because only it knows what is on disk.
    // keepScroll is for refreshing the list after a delete, so it does not jump back to the top.
    public void ShowWorldList(IReadOnlyList<WorldListEntry> worlds, bool keepScroll = false)
    {
        _worldList.SetWorlds(worlds, keepScroll);
        _showingWorldList = true;
    }

    // Opens the New World screen. Game1 calls this when New World is clicked, with the name a world gets if the
    // player types none ("World 3"), because only it knows what is on disk.
    public void ShowNewWorld(string defaultName)
    {
        _newWorld.Show(defaultName);
        _showingNewWorld = true;
    }

    // Shows a message over the menu for a few seconds. Game1 uses this when something the player asked for
    // (opening, creating or deleting a world) did not work.
    public void ShowNotice(string message) => _notice.Show(message);

    // Asks the player a question over the menu. Game1 uses this when something should not happen without an
    // explicit yes; exactly one of the two actions runs, once the player answers.
    public void AskConfirm(string heading, string message, string confirmLabel, string cancelLabel, Action onConfirm, Action onCancel) =>
        _confirm.Show(heading, message, confirmLabel, cancelLabel, onConfirm, onCancel);

    // A typed character from the window. Only the New World screen has text boxes.
    public void OnTextInput(char c)
    {
        if (_showingNewWorld) _newWorld.OnTextInput(c);
    }

    public void Update(ActionInput input, float deltaTime)
    {
        _panorama.Update(deltaTime);
        _notice.Update(deltaTime);

        // A question open: only its buttons respond
        if (_confirm.IsOpen)
        {
            _confirm.Update(input);
            return;
        }

        if (_showingNewWorld) _newWorld.Update(input, deltaTime);
        else if (_showingWorldList) _worldList.Update(input);
        else _current.Update(input);
    }

    public void Draw(SpriteBatch sprites, GraphicsDevice graphics)
    {
        int screenWidth = graphics.Viewport.Width;
        int screenHeight = graphics.Viewport.Height;

        // 1. Scrolling panorama (handles its own SpriteBatch)
        _panorama.Draw(sprites, graphics);

        // 2. UI on top
        sprites.Begin(samplerState: SamplerState.PointClamp);

        // The world list has its own heading, so the game's title is left out while it is showing
        if (_font != null)
        {
            // Everything is drawn at the GUI scale, so the title and version text grow with the buttons
            float scale = GuiScale.For(graphics);
            float titleScale = TitleScale * GuiScale.Text(scale);

            Vector2 titleSize = _font.MeasureString(GameInfo.Title);

            if (!_showingWorldList && !_showingNewWorld)
            {
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
            }

            // Left out on the world list, where the Back button would sit on top of it
            if (!_showingWorldList && !_showingNewWorld)
            {
                float versionScale = GuiScale.Text(scale);
                var versionPos = new Vector2(
                    GuiScale.Pixels(12 * scale),
                    screenHeight - GuiScale.Pixels((_font.LineSpacing + 8) * versionScale)
                );

                sprites.DrawString(
                    _font,
                    $"{GameInfo.Version} ({GameInfo.BuildType})",
                    versionPos,
                    new Color(90, 84, 122),
                    0f,
                    Vector2.Zero,
                    versionScale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        if (_showingNewWorld) _newWorld.Draw(sprites);
        else if (_showingWorldList) _worldList.Draw(sprites);
        else _current.Draw(sprites);

        _notice.Draw(sprites);
        _confirm.Draw(sprites);

        sprites.End();
    }
}