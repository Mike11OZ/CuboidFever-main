using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace CuboidFever;

// Which physical input triggers each action. Defaults live here; the JSON overrides come in a later step.
public sealed class KeyBindings
{
    private static readonly Dictionary<GameAction, Binding> Defaults = new()
    {
        [GameAction.MoveForward] = Keys.W,
        [GameAction.MoveBack]    = Keys.S,
        [GameAction.MoveLeft]    = Keys.A,
        [GameAction.MoveRight]   = Keys.D,
        [GameAction.Jump]        = Keys.Space,
        [GameAction.Descend]     = Keys.LeftShift,

        [GameAction.BreakBlock]  = Binding.FromMouse(GameMouseButton.Left),
        [GameAction.PlaceBlock]  = Binding.FromMouse(GameMouseButton.Right),
        [GameAction.Hotbar1] = Keys.D1, [GameAction.Hotbar2] = Keys.D2, [GameAction.Hotbar3] = Keys.D3,
        [GameAction.Hotbar4] = Keys.D4, [GameAction.Hotbar5] = Keys.D5, [GameAction.Hotbar6] = Keys.D6,
        [GameAction.Hotbar7] = Keys.D7, [GameAction.Hotbar8] = Keys.D8, [GameAction.Hotbar9] = Keys.D9,

        [GameAction.ToggleFly]        = Keys.V,
        [GameAction.CycleFpsCap]      = Keys.F,
        [GameAction.CycleWindowMode]  = Keys.F11,
        [GameAction.CycleLeafCulling] = Keys.L,
        [GameAction.ToggleAo]         = Keys.O,
        [GameAction.PauseTime]        = Keys.P,
        [GameAction.ScrubTimeBack]    = Keys.OemComma,
        [GameAction.ScrubTimeForward] = Keys.OemPeriod,
        [GameAction.ToggleDebugOverlay] = Keys.F3,

        [GameAction.Quit] = Keys.Escape,
    };

    private readonly Dictionary<GameAction, Binding> _map = new(Defaults);

    public Binding Get(GameAction action) => _map[action];

    public void Set(GameAction action, Binding binding) => _map[action] = binding;

    public void ResetToDefaults()
    {
        _map.Clear();
        foreach (var kv in Defaults) _map[kv.Key] = kv.Value;
    }
}
