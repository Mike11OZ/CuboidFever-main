using System;
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

    // ---- Game state ----
    private enum GameState
    {
        Startup,   // loading screen with progress bar while resources initialize
        MainMenu,  // title screen
        Spawning,  // loading screen while the world around the spawn point generates
        Playing,   // in the world
    }
    private GameState _state = GameState.Startup;

    private LoadingScreen _loadingScreen = null!; // shared by every phase that shows a loading screen
    private StartupScreen _startup = null!;       // runs the initialization steps while the loading screen is up
    private MainMenu _mainMenu = null!;

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
    }

    protected override void LoadContent()
    {
        // Only what the loading screen itself needs is created here, so it can be drawn right away.
        // Everything else is registered as a startup step and runs one step per frame (see StartupScreen),
        // which is what lets the progress bar move while resources load.
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
        _startup.AddStep("World and player", InitWorldAndPlayer);
        _startup.AddStep("Shaders and renderer", InitRenderer);
        _startup.AddStep("Screen effects and debug overlay", InitScreenEffectsAndDebugOverlay);
        _startup.AddStep("Main menu", InitMainMenu);
    }

    // =====================  STARTUP STEPS  =====================

    private void InitTextureAtlas()
    {
        TextureAtlas.Initialize(GraphicsDevice, GamePaths.BlockTextureRoot, GamePaths.TextureAtlasConfig);

        _particleSystem.Init(TextureAtlas.Instance);
    }

    private void InitWindowAndFonts()
    {
        _windowManager = new WindowManager(_graphics, Window);
        _windowManager.OnResolutionChanged += () => _renderer?.OnResize();

        // Subscribe to resize events so our 3D projection aspect ratio updates automatically
        Window.ClientSizeChanged += OnClientSizeChanged;

        try
        {
            _debugFont = Content.Load<SpriteFont>("DebugFont");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] DebugFont.xnb not found. On-screen debug overlay disabled. ({ex.Message})");
        }

        // If the menu font is missing, fall back to the (smaller) debug font rather than crashing
        try
        {
            _menuFont = Content.Load<SpriteFont>("MenuFont");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] MenuFont.xnb not found, using the debug font for menus. ({ex.Message})");
            _menuFont = _debugFont;
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
        _audioManager.Volume = 0.8f;
        _audioManager.InitializePlaylist(); // Preloads songs and sound effects
    }

    private void InitHotbarAndHud()
    {
        _hotbar = new Hotbar(BlockRegistry.Instance, _input);
        _hud = new Hud(GraphicsDevice, _sprites, TextureAtlas.Instance.Texture, _hotbar);
    }

    private void InitWorldAndPlayer()
    {
        _world = new World(715310);
        _hotkeys = new Hotkeys(_world, _dayNight, _windowManager, _fps, _screenshotManager, _input);
        _controller = new PlayerController(_player, _world, _input);

        _blockInteraction = new BlockInteraction(_world, _player, _controller, _hotbar, _input, _particleSystem, _dayNight, _audioManager);

        _spawn = new SpawnSequence(_world, _controller, _loadingScreen);
        _spawn.Spawned += OnSpawned;
    }

    private void InitRenderer()
    {
        var solidShader = Content.Load<Effect>("SolidShader");
        var waterShader = Content.Load<Effect>("WaterShader");
        _renderer = new WorldRenderer(GraphicsDevice, solidShader, waterShader, TextureAtlas.Instance.Texture,
                                      _world, _controller, _blockInteraction, _particleSystem, _dayNight);
    }

    private void InitScreenEffectsAndDebugOverlay()
    {
        _screenEffects = new ScreenEffectManager(GraphicsDevice, _sprites);
        _screenEffects.Add(new UnderwaterTint());

        // Built last because it reads from almost everything above
        _debugOverlay = new DebugOverlay(_debugFont, _sprites, _input, _world, _player, _fps, _hotbar,
                                         _windowManager, _dayNight, _audioManager, _blockInteraction);
    }

    private void InitMainMenu()
    {
        _mainMenu = new MainMenu(GraphicsDevice, _menuFont);
        _mainMenu.PlayRequested += StartGame;
        _mainMenu.SettingsRequested += OnSettingsRequested;
        _mainMenu.ExitRequested += Exit;
    }

    // =====================  STATE CHANGES  =====================

    private void EnterMainMenu()
    {
        _state = GameState.MainMenu;

        // Release the mouse so the cursor can be used to click buttons
        InputManager.SetMouseCaptured(false);
        IsMouseVisible = true;
    }

    // "Play Now": hide the cursor, recapture the mouse, and start generating the world.
    // The world only begins generating once it starts being updated, which happens in the Spawning state.
    private void StartGame()
    {
        InputManager.SetMouseCaptured(true);
        IsMouseVisible = false;
        _state = GameState.Spawning;
    }

    private void OnSettingsRequested()
    {
        // The settings menu is the next project; for now the button just reports the click.
        Console.WriteLine("[Menu] Settings menu not implemented yet.");
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
        _audioManager.StartPlaylist();
    }

    // =====================  UPDATE  =====================

    protected override void Update(GameTime gameTime)
    {
        float totalTime = (float)gameTime.TotalGameTime.TotalSeconds;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        var kb = Keyboard.GetState();
        var mouse = Mouse.GetState();
        _input.Update(kb, mouse);

        if (_input.WasPressed(GameAction.Quit)) Exit();

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
            _mainMenu.Update(_input);

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
        _hotkeys.Update(dt);
        _debugOverlay.Update();
        _dayNight.Update(dt);
        _world.SkyDarkness = _dayNight.SkyDarknessLevels;
        _controller.Update(dt, IsActive);
        _screenEffects.Update(dt, new ScreenEffectContext(_world, _controller.CamPos, totalTime));
        _hotbar.Update();
        _blockInteraction.Update(dt, IsActive);
        _particleSystem.Update(dt, _world);

        _world.Update(GraphicsDevice, _controller.CamPos, totalTime);
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

        // If the player hasn't spawned yet, render the loading screen instead of the world
        if (_state == GameState.Spawning)
        {
            _spawn.DrawSplash(_sprites, GraphicsDevice);
            base.Draw(gameTime);
            return;
        }

        _renderer.Draw(totalTime);
        _screenEffects.Draw(); // after the world, before the HUD so the crosshair and hotbar stay untinted
        _hud.Draw();
        _debugOverlay.UpdateFps(gameTime);
        _debugOverlay.Draw();

        base.Draw(gameTime);
    }
}