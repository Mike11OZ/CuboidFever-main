namespace CuboidFever;

// The toggle and debug shortcuts. It asks for actions, not keys, so rebinding needs no changes here.
public sealed class Hotkeys
{
    private const float TimeScrubHoursPerSecond = 3f;

    private readonly World _world;
    private readonly DayNightCycle _dayNight;
    private readonly FrameRateLimiter _fps;
    private readonly ScreenshotManager _screenshotManager;
    private readonly ActionInput _input;
    private readonly Hud _hud;

    // Note: the window mode hotkey is not here. These hotkeys only run while playing, and the window mode
    // has to work on every screen, so Game1 handles it.
    public Hotkeys(World world, DayNightCycle dayNight,
               FrameRateLimiter fps, ScreenshotManager screenshotManager,
               ActionInput input, Hud hud)
    {
        _world = world;
        _dayNight = dayNight;
        _fps = fps;
        _screenshotManager = screenshotManager;
        _input = input;
        _hud = hud;
    }

    public void Update(float dt)
    {
        if (_input.WasPressed(GameAction.ToggleHud))
            _hud.ToggleVisibility();

        if (_input.WasPressed(GameAction.CycleFpsCap)) _fps.CycleNext();
        if (_input.WasPressed(GameAction.CycleRenderDistance)) _world.CycleRenderDistance();
        
        // Trigger screenshot via action input system
        if (_input.WasPressed(GameAction.Screenshot)) _screenshotManager.CaptureScreenshot();

        if (_input.WasPressed(GameAction.CycleLeafCulling))
        {
            ChunkMesher.LeafCullDepth = (ChunkMesher.LeafCullDepth + 1) % (ChunkMesher.MaxLeafDepth + 1);
            _world.RemeshAll();
        }

        if (_input.WasPressed(GameAction.ToggleAo))
        {
            ChunkMesher.AmbientOcclusion = !ChunkMesher.AmbientOcclusion;
            _world.RemeshAll();
        }

        if (_input.WasPressed(GameAction.PauseTime)) _dayNight.Paused = !_dayNight.Paused;
        if (_input.IsDown(GameAction.ScrubTimeForward)) _dayNight.Scrub(TimeScrubHoursPerSecond * dt);
        if (_input.IsDown(GameAction.ScrubTimeBack)) _dayNight.Scrub(-TimeScrubHoursPerSecond * dt);
    }
}