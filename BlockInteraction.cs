using Microsoft.Xna.Framework;

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

    // Set when a right-click opened or closed a door, so holding the button does not go on to place blocks
    // (or flap the door) until it is released and pressed again.
    private bool _placeUsedOnDoor;

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

        if (!isPlacing) _placeUsedOnDoor = false;

        if (isPlacing)
        {
            _breakCooldown = 0f; // Reset break tracking when switching focus to placing

            if (_placeUsedOnDoor)
            {
                // Waiting for the button to be released
            }
            else if (_input.WasPressed(GameAction.PlaceBlock) && HasTarget)
            {
                if (TryUseTarget())
                {
                    _placeUsedOnDoor = true;
                }
                else
                {
                    Place();
                    _placeCooldown = PlaceInitialDelay; // Apply longer initial delay after the first placement
                }
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

        var info = BlockRegistry.Instance.Info;

        // A door is two blocks tall: breaking one half takes the other with it
        if (info[block].IsDoor && TryGetDoorPartner(x, y, z, out int partnerY))
        {
            byte partner = _world.GetBlock(x, partnerY, z);
            _world.SetBlock(x, partnerY, z, _airId);
            _particles.SpawnBurst(_world, x, partnerY, z, partner, _dayNight.SkyBrightness);
        }

        _world.SetBlock(x, y, z, _airId);
        _particles.SpawnBurst(_world, x, y, z, block, _dayNight.SkyBrightness);
        
        _audioManager?.PlaySound("break");
    }

    private void Place()
    {
        if (TargetPrev == Target) return;

        var (x, y, z) = TargetPrev;

        // The cell must be free: the ray can end in a door's empty space, and a door cannot be replaced
        byte existing = _world.GetBlock(x, y, z);
        if (existing != _airId && !BlockRegistry.Instance.Info[existing].IsWater) return;

        if (BlockRegistry.Instance.Info[_hotbar.SelectedId].IsDoor)
        {
            PlaceDoor(x, y, z);
            return;
        }

        if (_player.Overlaps(x, y, z)) return;

        _world.SetBlock(x, y, z, _hotbar.SelectedId);

        _audioManager?.PlaySound("place");
    }

    // A door fills two cells, the one you aimed at and the one above it, and faces the way the player is looking.
    private void PlaceDoor(int x, int y, int z)
    {
        if (y + 1 >= World.Height) return;

        var registry = BlockRegistry.Instance;
        var info = registry.Info;

        byte above = _world.GetBlock(x, y + 1, z);
        if (above != _airId && !info[above].IsWater) return;

        Vector3 look = _controller.Forward;
        string facing = BlockShapes.FacingNames[BlockShapes.FacingFromDirection(look.X, look.Z)];

        byte door = registry.GetDefaultState(_hotbar.SelectedId);
        door = registry.WithState(door, "facing", facing);
        byte lower = registry.WithState(door, "half", "lower");
        byte upper = registry.WithState(door, "half", "upper");

        // Don't shut the player inside their own door
        foreach (var (id, cellY) in new[] { (lower, y), (upper, y + 1) })
        {
            BlockShapes.DoorBox(in info[id], x, cellY, z, out var min, out var max);
            if (_player.OverlapsBox(min, max)) return;
        }

        _world.SetBlock(x, y, z, lower);
        _world.SetBlock(x, y + 1, z, upper);

        _audioManager?.PlaySound("place");
    }

    // Right-clicking a door opens or closes it instead of placing a block. Returns true if it did.
    private bool TryUseTarget()
    {
        var (x, y, z) = Target;
        byte block = _world.GetBlock(x, y, z);
        var info = BlockRegistry.Instance.Info;
        if (!info[block].IsDoor) return false;

        var registry = BlockRegistry.Instance;
        string open = info[block].DoorOpen ? "false" : "true";

        _world.SetBlock(x, y, z, registry.WithState(block, "open", open));

        // Keep the other half in step with this one
        if (TryGetDoorPartner(x, y, z, out int partnerY))
        {
            byte partner = _world.GetBlock(x, partnerY, z);
            _world.SetBlock(x, partnerY, z, registry.WithState(partner, "open", open));
        }

        // No dedicated door sounds yet, so reuse the block sounds
        _audioManager?.PlaySound(open == "true" ? "place" : "break", 0.6f);
        return true;
    }

    // The other half of the door at (x, y, z), if it is there and really is the matching half.
    private bool TryGetDoorPartner(int x, int y, int z, out int partnerY)
    {
        var info = BlockRegistry.Instance.Info;
        var self = info[_world.GetBlock(x, y, z)];

        partnerY = self.DoorUpper ? y - 1 : y + 1;
        if (partnerY < 0 || partnerY >= World.Height) return false;

        var other = info[_world.GetBlock(x, partnerY, z)];
        return other.IsDoor && other.DoorUpper != self.DoorUpper && other.DoorFacing == self.DoorFacing;
    }

    private void Select()
    {
        var (x, y, z) = Target;
        byte block = _world.GetBlock(x, y, z);
        if (block == _airId) return;
        _hotbar.TrySelectBlock(block);
    }
}