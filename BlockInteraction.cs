namespace CuboidFever;

public sealed class BlockInteraction
{
    private const float ReachDistance = 5f; 

    // Timing configurations
    private const float BreakInitialDelay = 0.4f;   // Delay after the first break before rapid breaking starts
    private const float BreakRepeatInterval = 0.15f; // Speed of rapid breaking once held
    private const float PlaceInitialDelay = 0.45f;  // Delay after the first place before rapid placing starts
    private const float PlaceRepeatInterval = 0.2f;   // Speed of rapid placing once held

    private readonly World _world;
    private readonly Player _player;
    private readonly PlayerController _controller;
    private readonly Hotbar _hotbar;
    private readonly ActionInput _input;
    private readonly ParticleSystem _particles;
    private readonly DayNightCycle _dayNight;
    private readonly AudioManager _audioManager;
    private readonly byte _airId;

    private float _breakCooldown;
    private float _placeCooldown;

    public BlockInteraction(World world, Player player, PlayerController controller, Hotbar hotbar,
                            ActionInput input, ParticleSystem particles, DayNightCycle dayNight, AudioManager audioManager)
    {
        _world = world;
        _player = player;
        _controller = controller;
        _hotbar = hotbar;
        _input = input;
        _particles = particles;
        _dayNight = dayNight;
        _audioManager = audioManager;
        _airId = BlockRegistry.Instance.GetId("cuboidfever:air");
    }

    public bool HasTarget { get; private set; }
    public (int x, int y, int z) Target { get; private set; }
    public (int x, int y, int z) TargetPrev { get; private set; }

    public void Update(float dt, bool windowActive)
    {
        HasTarget = _world.Raycast(_controller.CamPos, _controller.Forward, ReachDistance,
                                   out var target, out var targetPrev);
        Target = target;
        TargetPrev = targetPrev;

        if (!windowActive)
        {
            _breakCooldown = 0f;
            _placeCooldown = 0f;
            return;
        }

        if (_breakCooldown > 0) _breakCooldown -= dt;
        if (_placeCooldown > 0) _placeCooldown -= dt;

        if (_input.WasPressed(GameAction.SelectBlock)) Select();

        bool isPlacing = _input.IsDown(GameAction.PlaceBlock);
        bool isBreaking = _input.IsDown(GameAction.BreakBlock);

        if (isPlacing)
        {
            _breakCooldown = 0f; // Reset break tracking when switching focus to placing

            if (_input.WasPressed(GameAction.PlaceBlock) && HasTarget)
            {
                Place();
                _placeCooldown = PlaceInitialDelay; // Apply longer initial delay after the first placement
            }
            else if (_placeCooldown <= 0 && HasTarget)
            {
                Place();
                _placeCooldown = PlaceRepeatInterval; // Apply fast repeat interval for subsequent placements
            }
        }
        else if (isBreaking)
        {
            _placeCooldown = 0f; // Reset place tracking when switching focus to breaking

            if (_input.WasPressed(GameAction.BreakBlock) && HasTarget)
            {
                Break();
                _breakCooldown = BreakInitialDelay; // Apply longer initial delay after the first break
            }
            else if (_breakCooldown <= 0 && HasTarget)
            {
                Break();
                _breakCooldown = BreakRepeatInterval; // Apply fast repeat interval for subsequent breaks
            }
        }
        else
        {
            _breakCooldown = 0f;
            _placeCooldown = 0f;
        }
    }

    private void Break()
    {
        var (x, y, z) = Target;
        byte block = _world.GetBlock(x, y, z);
        if (block == _airId) return;

        _world.SetBlock(x, y, z, _airId);
        _particles.SpawnBurst(_world, x, y, z, block, _dayNight.SkyBrightness);
        
        _audioManager?.PlaySound("break");
    }

    private void Place()
    {
        if (TargetPrev == Target) return;

        var (x, y, z) = TargetPrev;
        if (_player.Overlaps(x, y, z)) return;

        _world.SetBlock(x, y, z, _hotbar.SelectedId);

        _audioManager?.PlaySound("place");
    }

    private void Select()
    {
        var (x, y, z) = Target;
        byte block = _world.GetBlock(x, y, z);
        if (block == _airId) return;
        _hotbar.TrySelectBlock(block);
    }
}