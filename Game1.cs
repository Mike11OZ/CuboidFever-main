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
    private SpriteBatch _sprites = null!; // still shared with StartupScreen and DebugOverlay

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
        TextureAtlas.Initialize(GraphicsDevice, GamePaths.BlockTextureRoot, GamePaths.TextureAtlasConfig);

        var atlas = TextureAtlas.Instance.Texture;

        _particleSystem.Init(TextureAtlas.Instance);

        _windowManager = new WindowManager(_graphics, Window);

        // Subscribe to resize events so our 3D projection aspect ratio updates automatically
        Window.ClientSizeChanged += OnClientSizeChanged;

        // Load your debug font from Content
        SpriteFont debugFont = null!;
        try
        {
            debugFont = Content.Load<SpriteFont>("DebugFont");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Warning] DebugFont.xnb not found. On-screen debug overlay disabled. ({ex.Message})");
        }

        _sprites = new SpriteBatch(GraphicsDevice);

        BlockRegistry.Initialize(GamePaths.DataRoot);
        InputManager.Initialize();
        _dayNight.LoadSettings(GamePaths.DayNightConfig);
        _dayNight.Reset();
// ---- Initialize Audio Handler ----
        _audioManager = new AudioManager();
        _audioManager.Volume = 0.8f;
        _audioManager.InitializePlaylist(); // Preloads songs and sound effects

        var blocks = BlockRegistry.Instance;

        _hotbar = new Hotbar(blocks, _input);
        _hud = new Hud(GraphicsDevice, _sprites, atlas, _hotbar);

        _world = new World(8374173);
        _hotkeys = new Hotkeys(_world, _dayNight, _windowManager, _fps, _input);
        _controller = new PlayerController(_player, _world, _input);
        
        // Pass _audioManager here:
        _blockInteraction = new BlockInteraction(_world, _player, _controller, _hotbar, _input, _particleSystem, _dayNight, _audioManager);

        _spawn = new SpawnSequence(_world, _controller);
        _spawn.LoadContent(GraphicsDevice);
        _spawn.Spawned += OnSpawned;

        var solidShader = Content.Load<Effect>("SolidShader");
        var waterShader = Content.Load<Effect>("WaterShader");
        _renderer = new WorldRenderer(GraphicsDevice, solidShader, waterShader, atlas,
                                      _world, _controller, _blockInteraction, _particleSystem, _dayNight);

        _screenEffects = new ScreenEffectManager(GraphicsDevice, _sprites);
        _screenEffects.Add(new UnderwaterTint());

        // Built last because it reads from almost everything above
        _debugOverlay = new DebugOverlay(debugFont, _sprites, _input, _world, _player, _fps, _hotbar,
                                         _windowManager, _dayNight, _audioManager, _blockInteraction);
    }

    // Runs once, right after the player is placed in the world and gameplay begins.
    private void OnSpawned()
    {
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

        // Tick the audio handler every frame
        _audioManager.Update(gameTime);

        if (!_spawn.HasSpawned)
        {
            _world.Update(GraphicsDevice, _controller.CamPos, totalTime);
            _spawn.Update(dt);

            base.Update(gameTime);
            return;
        }
        _hotkeys.Update(dt);
        _debugOverlay.Update(gameTime);
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

    private void OnClientSizeChanged(object sender, EventArgs e) => _renderer?.OnResize();

    // =====================  DRAW  =====================

    protected override void Draw(GameTime gameTime)
    {
        float totalTime = (float)gameTime.TotalGameTime.TotalSeconds;

        // If the player hasn't spawned yet, render the startup screen instead of the world
        if (!_spawn.HasSpawned)
        {
            _spawn.DrawSplash(_sprites, GraphicsDevice);
            base.Draw(gameTime);
            return;
        }

        _renderer.Draw(totalTime);
        _screenEffects.Draw(); // after the world, before the HUD so the crosshair and hotbar stay untinted
        _hud.Draw();
        _debugOverlay.Draw();

        base.Draw(gameTime);
    }
}