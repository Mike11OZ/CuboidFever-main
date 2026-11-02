namespace CuboidFever;

// Finds the block the player is looking at and handles breaking and placing it.
public sealed class BlockInteraction
{
    private const float ReachDistance = 5f; // Easy to tweak here!

    private readonly World _world;
    private readonly Player _player;
    private readonly PlayerController _controller;
    private readonly Hotbar _hotbar;
    private readonly ActionInput _input;
    private readonly ParticleSystem _particles;
    private readonly DayNightCycle _dayNight;
    private readonly byte _airId;

    public BlockInteraction(World world, Player player, PlayerController controller, Hotbar hotbar,
                            ActionInput input, ParticleSystem particles, DayNightCycle dayNight)
    {
        _world = world;
        _player = player;
        _controller = controller;
        _hotbar = hotbar;
        _input = input;
        _particles = particles;
        _dayNight = dayNight;
        _airId = BlockRegistry.Instance.GetId("cuboidfever:air");
    }

    // The block being looked at, and the empty cell in front of it (where a placed block would go).
    public bool HasTarget { get; private set; }
    public (int x, int y, int z) Target { get; private set; }
    public (int x, int y, int z) TargetPrev { get; private set; }

    // 'windowActive' stops clicks from acting while the window is unfocused.
    public void Update(bool windowActive)
    {
        HasTarget = _world.Raycast(_controller.CamPos, _controller.Forward, ReachDistance,
                                   out var target, out var targetPrev);
        Target = target;
        TargetPrev = targetPrev;

        if (!windowActive || !HasTarget) return;

        if (_input.WasPressed(GameAction.BreakBlock)) Break();
        if (_input.WasPressed(GameAction.PlaceBlock)) Place();
    }

    private void Break()
    {
        var (x, y, z) = Target;
        byte block = _world.GetBlock(x, y, z);
        if (block == _airId) return;

        _world.SetBlock(x, y, z, _airId);
        _particles.SpawnBurst(_world, x, y, z, block, _dayNight.SkyBrightness);
    }

    private void Place()
    {
        if (TargetPrev == Target) return;

        var (x, y, z) = TargetPrev;
        if (_player.Overlaps(x, y, z)) return; // don't place a block inside yourself

        _world.SetBlock(x, y, z, _hotbar.SelectedId);
    }
}
