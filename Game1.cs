using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CuboidFever;

public class Game1 : Game
{
    // ---- Core ----
    private GraphicsDeviceManager _graphics;
    private ParticleSystem _particleSystem = new();
    private World _world = null!;
    private SpawnSequence _spawn = null!;
    private WindowManager _windowManager = null!;
    private DebugOverlay _debugOverlay = null!;
    private readonly DayNightCycle _dayNight = new();
    private Hotkeys _hotkeys = null!;
    private readonly FrameRateLimiter _fps;
    private readonly ActionInput _input = new(new KeyBindings());
    private ScreenshotManager _screenshotManager = null!;
    private ScreenshotPanoramicManager _panoramicManager = null!;

    // ---- Game state ----
    private enum GameState
    {
        Startup,   // loading screen with progress bar while resources initialize
        MainMenu,  // title screen
        Spawning,  // loading screen while the world around the spawn point generates
        Playing,   // in the world
        Paused,    // in the world, frozen, with the pause menu over it
        Settings,  // settings screen, opened from the main menu or the pause menu
    }
    private GameState _state = GameState.Startup;

    // Where Back goes from the settings screen: MainMenu or Paused, whichever opened it
    private GameState _settingsReturnState = GameState.MainMenu;

    private const int WorldSeed = 706944;

    // The saved world being played. Null before a world starts, and for a world that could not be given a save
    // (it then plays as it did before saving existed: nothing is written).
    private WorldSave _worldSave;

    // Set when the world list picture should be taken. DrawWorld takes it after the world is drawn but before the
    // HUD, so the picture shows the view without the crosshair and hotbar.
    private bool _thumbnailPending;

    // How often level.json (position, time of day, hotbar) is rewritten while playing, so a crash loses at most
    // this much movement. Edited columns have their own autosave (World.UpdateAutosave).
    private const float LevelAutosaveSeconds = 15f;
    private readonly IntervalTimer _levelAutosave = new(LevelAutosaveSeconds);

    // Messages shown while playing, such as the missing-blocks warning
    private NoticeBanner _gameNotice = null!;

    private LoadingScreen _loadingScreen = null!; // shared by every phase that shows a loading screen
    private StartupScreen _startup = null!;       // runs the initialization steps while the loading screen is up
    private MainMenu _mainMenu = null!;
    private PauseMenu _pauseMenu = null!;
    private SettingsMenu _settingsMenu = null!;

    // ---- Audio ----
    private AudioManager _audioManager = null!;
    private const float SongLoopDelay = 4.0f; // Configurable delay between loops

    // ---- Camera ----
    private Player _player = new();
    private PlayerController _controller = null!;

    // ---- Block interaction ----
    private Hotbar _hotbar = null!;
    private BlockInteraction _blockInteraction = null!;

    // ---- Rendering ----
    private WorldRenderer _renderer = null!;
    private ScreenEffectManager _screenEffects = null!; // full-screen effects: underwater tint, later damage flash, etc.
    private Hud _hud = null!;
    private SpriteBatch _sprites = null!; // shared with LoadingScreen, Hud and DebugOverlay
    private SpriteFont _debugFont = null!;
    private SpriteFont _menuFont = null!; // larger font for menus and buttons
    private SpriteFont _menuFontSmall = null!; // half the size: for text inside list rows and dialogs
    private Effect _solidShader = null!;  // loaded once at startup, reused by every world's renderer
    private Effect _waterShader = null!;
    

    public Game1()
    {
        DebugConsole.Init();
        Console.WriteLine($"Initializing {GameInfo.FullTitle}...");

        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        _graphics.SynchronizeWithVerticalRetrace = false;
        _fps = new FrameRateLimiter(this);
        _fps.Apply();

        // Closing the window, Exit Game and the main menu's Quit all end up here
        Exiting += (_, _) => CloseWorldSave();

        // Typed characters (in the New World screen's text boxes). The window turns key presses into characters
        // for the player's keyboard layout; nothing else in the game needs them.
        Window.TextInput += (_, e) =>
        {
            if (_state == GameState.MainMenu) _mainMenu.OnTextInput(e.Character);
        };
    }

    protected override void LoadContent()
    {
        // Only what the loading screen itself needs is created here, so it can be drawn right away.
        // Everything else is registered as a startup step and runs one step per frame (see StartupScreen),
        // which is what lets the progress bar advance while resources load.
        _sprites = new SpriteBatch(GraphicsDevice);

        _loadingScreen = new LoadingScreen();
        _loadingScreen.LoadContent(GraphicsDevice);

        _startup = new StartupScreen(_loadingScreen);

        // Order matters: each step may rely on the ones before it.
        // Weights are all 1 for now. The log line printed after each step shows its time in ms,
        // so slow steps can be given a bigger weight to make the bar advance more evenly.
        _startup.AddStep("Texture atlas", InitTextureAtlas);
        _startup.AddStep("Window and fonts", InitWindowAndFonts);
        _startup.AddStep("Block registry", InitBlockRegistry);
        _startup.AddStep("Input and day/night", InitInputAndDayNight);
        _startup.AddStep("Audio", InitAudio);
        _startup.AddStep("Hotbar and HUD", InitHotbarAndHud);
        _startup.AddStep("Shaders", InitShaders);
        _startup.AddStep("Screen effects", InitScreenEffects);
        _startup.AddStep("Menus", InitMenus);

        // The world, player, renderer and everything else that belongs to one particular world is not built
        // here. It is built when the player starts a world (CreateWorldSession) and thrown away on
        // Exit World (DestroyWorldSession), so a player can leave a world and start a fresh one.
    }

    // =====================  STARTUP STEPS  =====================

    private void InitTextureAtlas()
    {
        TextureAtlas.Initialize(GraphicsDevice, GamePaths.BlockTextureRoot, GamePaths.TextureAtlasConfig);
    }

    private void InitWindowAndFonts()
    {
        _windowManager = new WindowManager(_graphics, Window);
        _windowManager.OnResolutionChanged += () => _renderer?.OnResize();

        // Subscribe to resize events so our 3D projection aspect ratio updates automatically
        Window.ClientSizeChanged += OnClientSizeChanged;

        try
        {
            _debugFont = Content.Load<SpriteFont>("Fonts/DebugFont");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] DebugFont.xnb not found. On-screen debug overlay disabled. ({ex.Message})");
        }

        // If the menu font is missing, fall back to the (smaller) debug font rather than crashing
        try
        {
            _menuFont = Content.Load<SpriteFont>("Fonts/MenuFont");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] MenuFont.xnb not found, using the debug font for menus. ({ex.Message})");
            _menuFont = _debugFont;
        }

        // The small menu font is optional too: without it the list screens simply use the larger one
        try
        {
            _menuFontSmall = Content.Load<SpriteFont>("Fonts/MenuFontSmall");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] MenuFontSmall.xnb not found, using the menu font. ({ex.Message})");
            _menuFontSmall = _menuFont;
        }
    }

    private void InitBlockRegistry()
    {
        BlockRegistry.Initialize(GamePaths.DataRoot);
    }

    private void InitInputAndDayNight()
    {
        InputManager.Initialize();
        _dayNight.LoadSettings(GamePaths.DayNightConfig);
        _dayNight.Reset();

        // Initialize screenshot manager using the graphics device
        _screenshotManager = new ScreenshotManager(GraphicsDevice);
    }

    private void InitAudio()
    {
        _audioManager = new AudioManager();
        _audioManager.MasterVolume = 1.0f;
        _audioManager.MusicVolume = 0.8f;
        _audioManager.InitializePlaylist(); // Preloads songs and sound effects
    }

    private void InitHotbarAndHud()
    {
        _hotbar = new Hotbar(BlockRegistry.Instance, _input);
        _hud = new Hud(GraphicsDevice, _sprites, TextureAtlas.Instance.Texture, _hotbar);
    }

    private void InitShaders()
    {
        _solidShader = Content.Load<Effect>("SolidShader");
        _waterShader = Content.Load<Effect>("WaterShader");
    }

    private void InitScreenEffects()
    {
        _screenEffects = new ScreenEffectManager(GraphicsDevice, _sprites);
        _screenEffects.Add(new UnderwaterTint());
    }

    private void InitMenus()
    {
        _gameNotice = new NoticeBanner(GraphicsDevice, _menuFontSmall);

        _mainMenu = new MainMenu(
            GraphicsDevice,
            _menuFont,
            _menuFontSmall,
            _audioManager.PlayMenuSound
        );

        _mainMenu.NewWorldRequested += OnNewWorldRequested;
        _mainMenu.CreateWorldRequested += OnCreateWorldRequested;
        _mainMenu.LoadWorldRequested += OnLoadWorldRequested;
        _mainMenu.WorldSelected += OnWorldSelected;
        _mainMenu.DeleteWorldRequested += OnDeleteWorldRequested;
        _mainMenu.SettingsRequested += OnSettingsRequested;
        _mainMenu.ExitRequested += Exit;

        _pauseMenu = new PauseMenu(
            GraphicsDevice,
            _menuFont,
            _audioManager.PlayMenuSound
        );

        _pauseMenu.ResumeRequested += ResumeGame;
        _pauseMenu.SettingsRequested += OnSettingsRequested;
        _pauseMenu.ExitWorldRequested += ExitWorld;
        _pauseMenu.ExitGameRequested += Exit;

        _settingsMenu = new SettingsMenu(
            GraphicsDevice,
            _menuFont,
            _fps,
            _windowManager,
            _audioManager,
            CycleRenderDistance,
            OnMeshSettingChanged,
            _audioManager.PlayMenuSound
        );

        _settingsMenu.BackRequested += CloseSettings;
    }

    // =====================  WORLD SESSION  =====================
    //
    // Everything that belongs to one particular world: the world itself, the player in it, and the classes
    // that were built holding a reference to either. Each time a world is started makes a complete new set, and Exit World
    // throws the set away, so nothing from one world (blocks, particles, player position, time of day) can
    // leak into the next. Things that don't depend on a world (settings, fonts, shaders, the hotbar, the
    // audio manager) are built once at startup instead.

    private void CreateWorldSession(WorldSave save)
    {
        _dayNight.Reset();
        if (save != null)
        {
            _dayNight.TimeOfDay = save.Info.TimeOfDay;
            _dayNight.Scrub(0f); // brings the sky colors in line with the restored time
        }
        _player = new Player();

        _particleSystem = new ParticleSystem();
        _particleSystem.Init(TextureAtlas.Instance);

        _world = new World(save?.Info.Seed ?? WorldSeed, save);
        _controller = new PlayerController(_player, _world, _input);

        // Restore where the player was, how they were looking, and what they had selected
        Vector3? savedFeet = null;
        var savedPlayer = save?.Info.Player;
        if (savedPlayer != null)
        {
            _player.Flying = savedPlayer.Flying;
            _controller.SetLook(savedPlayer.Yaw, savedPlayer.Pitch);
            _hotbar.SelectSlot(savedPlayer.SelectedHotbarSlot);

            if (savedPlayer.HasUsablePosition())
            {
                savedFeet = new Vector3(savedPlayer.X, savedPlayer.Y, savedPlayer.Z);
                _controller.SetLoadingCamera(savedFeet.Value);   // the world loads around the camera
            }
        }

        _blockInteraction = new BlockInteraction(_world, _player, _controller, _hotbar, _input, _particleSystem, _dayNight, _audioManager);

        _renderer = new WorldRenderer(GraphicsDevice, _solidShader, _waterShader, TextureAtlas.Instance.Texture,
                                    _world, _controller, _blockInteraction, _particleSystem, _dayNight);

        // Initialized before Hotkeys so it is never null
        _panoramicManager = new ScreenshotPanoramicManager(GraphicsDevice, _renderer);

        _hotkeys = new Hotkeys(
            _world,
            _dayNight,
            _fps,
            _screenshotManager,
            _panoramicManager,
            _controller,
            _input,
            _hud
        );

        _spawn = new SpawnSequence(_world, _controller, _loadingScreen, savedFeet);
        _spawn.Spawned += OnSpawned;

        // Built last because it reads from almost everything above
        _debugOverlay = new DebugOverlay(_debugFont, _sprites, _input, _world, _player, _fps, _hotbar,
                                         _windowManager, _dayNight, _audioManager, _blockInteraction);
    }

    private void DestroyWorldSession()
    {
        SaveWorld(wait: true); // before the world is disposed, because this needs its columns

        _thumbnailPending = false;
        _world.Dispose(); // stops its worker threads and frees its chunk meshes

        // The world's workers have stopped, so nothing can read from the save any more
        _worldSave?.Dispose();
        _worldSave = null;

        _world = null!;
        _player = null!;
        _particleSystem = null!;
        _hotkeys = null!;
        _controller = null!;
        _blockInteraction = null!;
        _spawn = null!;
        _renderer = null!;
        _panoramicManager = null!;
        _debugOverlay = null!;
    }

    // Writes the world's edited columns and level.json. With wait: true it also blocks until everything has
    // reached the disk (used when the world is being left); without it the writing carries on in the background
    // (used on pause, where a stall would be noticeable). Does nothing if there is no world or no save.
    private void SaveWorld(bool wait)
    {
        if (_worldSave == null || _world == null) return;

        _world.SaveAll();
        SaveLevelNow();

        if (wait && !_worldSave.Flush())
            Console.Error.WriteLine("[Save] Some columns could not be written to disk. See the errors above.");
    }

    // Writes level.json from the game as it is right now: time of day, and where the player is and what they hold.
    private void SaveLevelNow()
    {
        if (_worldSave == null || _world == null) return;

        _worldSave.Info.TimeOfDay = _dayNight.TimeOfDay;

        // Only once the player has been placed: until then they are at the loading camera, not anywhere real,
        // and saving that would overwrite where they really were
        if (_spawn != null && _spawn.HasSpawned)
        {
            _worldSave.Info.Player = new PlayerSave
            {
                X = _player.Position.X,
                Y = _player.Position.Y,
                Z = _player.Position.Z,
                Yaw = _controller.Yaw,
                Pitch = _controller.Pitch,
                Flying = _player.Flying,
                SelectedHotbarSlot = _hotbar.Slot
            };
        }

        try
        {
            _worldSave.SaveLevel();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[Save] Could not write level.json: {e.Message}");
        }
    }

    // The game is closing (window closed, Exit Game, Quit): save the world if one is open.
    private void CloseWorldSave()
    {
        if (_world == null) return;

        // Closing while playing (no pause first): if the world has no picture yet, use the last frame on screen.
        // An existing picture is kept, because the back buffer is not guaranteed to still hold a good frame here.
        if (_state == GameState.Playing && _worldSave != null && !File.Exists(WorldThumbnail.PathIn(_worldSave.FolderPath)))
            _worldSave.SaveThumbnail(ThumbnailCapture.Capture(GraphicsDevice));

        SaveWorld(wait: true);
        _worldSave?.Dispose();
        _worldSave = null;
    }

    // =====================  STATE CHANGES  =====================

    private void EnterMainMenu()
    {
        _state = GameState.MainMenu;
        _mainMenu.ShowMainPage();

        _audioManager.PlayTitleMusic();

        // Release the mouse so the cursor can be used to click buttons
        InputManager.SetMouseCaptured(false);
        IsMouseVisible = true;
    }

    // The solo menu's world buttons. New World opens the screen where the name and seed are chosen, and Create World
    // on that screen makes the saved world. Load World shows the list of saved worlds.
    private void OnNewWorldRequested() => _mainMenu.ShowNewWorld(WorldSave.NextDefaultName());

    // Create World was pressed with this name and seed text. A blank name becomes "World N", a blank seed a random
    // one, and a text seed always gives the same world (NewWorldRules).
    private void OnCreateWorldRequested(string nameText, string seedText)
    {
        WorldSave save = null;

        try
        {
            string name = NewWorldRules.CleanName(nameText, WorldSave.NextDefaultName());
            int seed = NewWorldRules.ParseSeed(seedText, () => Random.Shared.Next());

            save = WorldSave.CreateNew(name, seed);
        }
        catch (Exception e)
        {
            // Stay on the New World screen. Starting the world anyway would play a world that is never saved,
            // which the player would only find out about after losing it.
            Console.Error.WriteLine($"[Save] Could not create the world: {e.Message}");
            _mainMenu.ShowNotice($"Could not create the world: {e.Message}");
            return;
        }

        StartGame(save);
    }

    // Load World: look at what is on disk (most recently played first) and show it as a list.
    private void OnLoadWorldRequested() => ShowSavedWorlds(keepScroll: false);

    // The player confirmed deleting a world from the list. The list is shown again either way, so it always
    // matches what is on disk. A failure is shown as a message on the menu.
    private void OnDeleteWorldRequested(string folderPath)
    {
        try
        {
            WorldSave.DeleteWorld(folderPath);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[Save] Could not delete '{folderPath}': {e.Message}");
            _mainMenu.ShowNotice($"Could not delete the world: {e.Message}");
        }

        ShowSavedWorlds(keepScroll: true);
    }

    private void ShowSavedWorlds(bool keepScroll)
    {
        var entries = new List<WorldListEntry>();

        try
        {
            foreach (var world in WorldSave.ListWorlds())
                entries.Add(new WorldListEntry(world.FolderPath, world.Info.Name, world.Info.LastPlayedUtc));
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"[Save] Could not list the saved worlds: {e.Message}");
        }

        _mainMenu.ShowWorldList(entries, keepScroll);
    }

    // A world was picked from the list.
    private void OnWorldSelected(string folderPath)
    {
        WorldSave save;

        try
        {
            save = WorldSave.Open(folderPath);
        }
        catch (Exception e)
        {
            // Stay on the list. Starting some other world instead would look as if the chosen one had been lost.
            Console.Error.WriteLine($"[Save] Could not open the world in '{folderPath}': {e.Message}");
            _mainMenu.ShowNotice($"Could not open this world: {e.Message}");
            return;
        }

        // A world with blocks this version lacks must not open by surprise: they would show as air, and saving
        // over them loses them for good. Ask first; the world stays opened (and locked) until the answer.
        string question = MissingBlocksMessage.MakeQuestion(save.MissingBlocks);
        if (question != null)
        {
            _mainMenu.AskConfirm("Missing blocks", question, "Open Anyway", "Cancel",
                onConfirm: () => StartGame(save),
                onCancel: () => save.Dispose());
            return;
        }

        StartGame(save);
    }

    // Build the world (from a save, or without one), hide the cursor, recapture the mouse, and start generating.
    // The world only begins generating once it starts being updated, which happens in the Spawning state.
    private void StartGame(WorldSave save)
    {
        _worldSave = save;
        CreateWorldSession(save);
        _levelAutosave.Reset();

        string missing = save == null ? null : MissingBlocksMessage.Make(save.MissingBlocks);
        if (missing != null) _gameNotice.Show(missing, 10f); // a long message: give it time to be read

        InputManager.SetMouseCaptured(true);
        IsMouseVisible = false;
        _state = GameState.Spawning;
    }

    // Esc or the pause menu's "Resume Game": the world freezes in place and the cursor comes back.
    private void PauseGame()
    {
        _state = GameState.Paused;

        // The world is frozen, so this is a good moment to bring the save fully up to date
        SaveWorld(wait: false);

        // The paused frame is drawn next. It is the last view of the world before the player leaves it, so it
        // becomes the world's picture in the list.
        _thumbnailPending = true;

        InputManager.SetMouseCaptured(false);
        IsMouseVisible = true;
    }

    private void ResumeGame()
    {
        _state = GameState.Playing;

        InputManager.SetMouseCaptured(true);
        IsMouseVisible = false;

        // Throw away the mouse movement that built up while paused, so the camera doesn't jump
        InputManager.GetMouseDelta(out _, out _);
        Mouse.SetPosition(Window.ClientBounds.Width / 2, Window.ClientBounds.Height / 2);
    }

    // The pause menu's "Save and Quit to Title": save and discard the world and go back to the title screen.
    private void ExitWorld()
    {
        DestroyWorldSession();

        // Stop the music so it doesn't carry over to the title screen. Starting a new world restarts it.
        _audioManager.Stop();

        EnterMainMenu();
    }

    // "Settings" from the main menu or the pause menu. Remember which, so Back returns there.
    private void OnSettingsRequested()
    {
        _settingsReturnState = _state;
        _settingsMenu.ShowCategoryPage();   // ← add this line
        _state = GameState.Settings;
    }

    private void CloseSettings()
    {
        _state = _settingsReturnState;
    }

    // Render distance is a player setting that exists before any world does (it can be set from the main
    // menu), so with no world it only changes the setting. A running world also applies it straight away.
    private void CycleRenderDistance()
    {
        if (_world != null) _world.CycleRenderDistance();
        else World.AdvanceRenderDistanceSetting();
    }

    // Ambient occlusion and leaf culling change how chunks are built, so a running world has to rebuild its
    // meshes. With no world the new value is simply used when the next world is built. While paused the
    // world is frozen, so the rebuild shows once the game is resumed.
    private void OnMeshSettingChanged()
    {
        _world?.RemeshAll();
    }

    // Runs once, right after the player is placed in the world and gameplay begins.
    private void OnSpawned()
    {
        _state = GameState.Playing;

        // Throw away any mouse movement that built up while the cursor was captured during loading,
        // so the camera doesn't jump on the first frame
        InputManager.GetMouseDelta(out _, out _);
        Mouse.SetPosition(Window.ClientBounds.Width / 2, Window.ClientBounds.Height / 2);

        // Kick off the initial random delay timer (1 to 5 minutes) upon spawning
        _audioManager.PlayGameMusic();
    }

    // =====================  UPDATE  =====================

    protected override void Update(GameTime gameTime)
    {
        float totalTime = (float)gameTime.TotalGameTime.TotalSeconds;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        var kb = Keyboard.GetState();
        var mouse = Mouse.GetState();
        _input.Update(kb, mouse);

        _gameNotice?.Update(dt);

        // Window mode works on every screen (menus included), so it's handled here rather than in Hotkeys,
        // which only runs while playing. The window manager is created early in startup.
        if (_windowManager != null && _input.WasPressed(GameAction.CycleWindowMode))
            _windowManager.CycleNextMode();

        // Startup: run the next initialization step. Almost nothing exists yet (no world, no audio),
        // so nothing else updates until every step has run.
        if (_state == GameState.Startup)
        {
            _startup.Update();
            if (_startup.IsComplete) EnterMainMenu();

            base.Update(gameTime);
            return;
        }

        // Tick the audio handler every frame
        _audioManager.Update(gameTime);

        if (_state == GameState.MainMenu)
        {
            _mainMenu.Update(_input, (float)gameTime.ElapsedGameTime.TotalSeconds);

            base.Update(gameTime);
            return;
        }

        // Settings: Esc goes back, same as the Back button
        if (_state == GameState.Settings)
        {
            // SettingsMenu now handles Escape itself (sub-page vs root)
            _settingsMenu.Update(_input);

            base.Update(gameTime);
            return;
        }

        if (_state == GameState.Spawning)
        {
            _world.Update(GraphicsDevice, _controller.CamPos, totalTime);
            _spawn.Update(dt);

            base.Update(gameTime);
            return;
        }

        // Paused: the world is frozen (nothing below runs), only the pause menu responds.
        if (_state == GameState.Paused)
        {
            if (_input.WasPressed(GameAction.Pause))
            {
                _audioManager.PlayMenuSound(MenuButtonSound.Back);
                ResumeGame();
            }
            else
            {
                _pauseMenu.Update(_input);
            }

            base.Update(gameTime);
            return;
        }

        // Playing
        if (_input.WasPressed(GameAction.Pause))
        {
            _audioManager.PlayMenuSound(MenuButtonSound.Click);
            PauseGame();

            base.Update(gameTime);
            return;
        }

        _hotkeys.Update(dt, totalTime);
        _debugOverlay.Update();
        _dayNight.Update(dt);
        _world.SkyDarkness = _dayNight.SkyDarknessLevels;
        _controller.Update(dt, IsActive);
        _screenEffects.Update(dt, new ScreenEffectContext(_world, _controller.CamPos, totalTime));
        _hotbar.Update();
        _blockInteraction.Update(dt, IsActive);
        _particleSystem.Update(dt, _world);

        _world.Update(GraphicsDevice, _controller.CamPos, totalTime);

        // Position and the rest of level.json, so a crash does not lose recent movement
        if (_levelAutosave.Advance(dt))
            SaveLevelNow();

        base.Update(gameTime);
    }

    private void OnClientSizeChanged(object sender, EventArgs e)
    {
        _renderer?.OnResize();
    }

    // =====================  DRAW  =====================

    protected override void Draw(GameTime gameTime)
    {
        float totalTime = (float)gameTime.TotalGameTime.TotalSeconds;

        // Startup: show the loading screen with its progress bar while resources initialize
        if (_state == GameState.Startup)
        {
            _startup.Draw(_sprites, GraphicsDevice);
            base.Draw(gameTime);
            return;
        }

        if (_state == GameState.MainMenu)
        {
            _mainMenu.Draw(_sprites, GraphicsDevice);
            base.Draw(gameTime);
            return;
        }

        // Settings: over the frozen world if it was opened from the pause menu, otherwise on its own
        if (_state == GameState.Settings)
        {
            bool overWorld = _settingsReturnState == GameState.Paused;
            if (overWorld) DrawWorld(gameTime, totalTime);

            _settingsMenu.Draw(_sprites, GraphicsDevice, overWorld);
            base.Draw(gameTime);
            return;
        }

        // If the player hasn't spawned yet, render the loading screen instead of the world
        if (_state == GameState.Spawning)
        {
            _spawn.DrawSplash(_sprites, GraphicsDevice);

            // The loading splash can take a few seconds; the world's warnings should not run out behind it
            _sprites.Begin(samplerState: SamplerState.PointClamp);
            _gameNotice.Draw(_sprites);
            _sprites.End();

            base.Draw(gameTime);
            return;
        }

        // Playing or Paused: the world is drawn the same either way. When paused it simply stops changing.
        DrawWorld(gameTime, totalTime);

        // The pause menu dims everything drawn above it
        if (_state == GameState.Paused)
            _pauseMenu.Draw(_sprites, GraphicsDevice);

        _sprites.Begin(samplerState: SamplerState.PointClamp);
        _gameNotice.Draw(_sprites);
        _sprites.End();

        base.Draw(gameTime);
    }

    private void DrawWorld(GameTime gameTime, float totalTime)
    {
        _renderer.Draw(totalTime);
        _screenEffects.Draw(); // after the world, before the HUD so the crosshair and hotbar stay untinted

        if (_thumbnailPending)
        {
            _thumbnailPending = false;
            _worldSave?.SaveThumbnail(ThumbnailCapture.Capture(GraphicsDevice));
        }

        _hud.Draw();
        _debugOverlay.UpdateFps(gameTime);
        _debugOverlay.Draw();
    }
}