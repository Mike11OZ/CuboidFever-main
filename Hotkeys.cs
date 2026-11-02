namespace CuboidFever;

// The toggle and debug shortcuts. It asks for actions, not keys, so rebinding needs no changes here.
public sealed class Hotkeys
{
    private const float TimeScrubHoursPerSecond = 3f;

    private readonly World _world;
    private readonly DayNightCycle _dayNight;
    private readonly WindowManager _windowManager;
    private readonly FrameRateLimiter _fps;
    private readonly ActionInput _input;

    public Hotkeys(World world, DayNightCycle dayNight, WindowManager windowManager,
                   FrameRateLimiter fps, ActionInput input)
    {
        _world = world;
        _dayNight = dayNight;
        _windowManager = windowManager;
        _fps = fps;
        _input = input;
    }

    public void Update(float dt)
    {
        if (_input.WasPressed(GameAction.CycleFpsCap)) _fps.CycleNext();
        if (_input.WasPressed(GameAction.CycleWindowMode)) _windowManager.CycleNextMode();

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
