namespace CuboidFever;

public sealed class Hotkeys
{
    private const float TimeScrubHoursPerSecond = 3f;

    private readonly World _world;
    private readonly DayNightCycle _dayNight;
    private readonly FrameRateLimiter _fps;
    private readonly ScreenshotManager _screenshotManager;
    private readonly ScreenshotPanoramicManager _panoramicManager;
    private readonly PlayerController _controller;
    private readonly ActionInput _input;
    private readonly Hud _hud;

    public Hotkeys(World world, DayNightCycle dayNight,
                   FrameRateLimiter fps, ScreenshotManager screenshotManager,
                   ScreenshotPanoramicManager panoramicManager, PlayerController controller,
                   ActionInput input, Hud hud)
    {
        _world = world;
        _dayNight = dayNight;
        _fps = fps;
        _screenshotManager = screenshotManager;
        _panoramicManager = panoramicManager;
        _controller = controller;
        _input = input;
        _hud = hud;
    }

    public void Update(float dt, float totalTime)
    {
        if (_input.WasPressed(GameAction.ToggleHud))
            _hud.ToggleVisibility();

        if (_input.WasPressed(GameAction.CycleFpsCap)) _fps.CycleNext();
        if (_input.WasPressed(GameAction.CycleRenderDistance)) _world.CycleRenderDistance();
        
        if (_input.WasPressed(GameAction.Screenshot)) _screenshotManager.CaptureScreenshot();
        
        if (_input.WasPressed(GameAction.ScreenshotPanoramic))
            _panoramicManager.CapturePanoramicScreenshot(_controller.CamPos, _controller.Forward, totalTime);

        if (_input.WasPressed(GameAction.CycleLeafCulling))
        {
            ChunkMesher.LeafCullDepth = (ChunkMesher.LeafCullDepth + 1) / (ChunkMesher.MaxLeafDepth + 1);
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