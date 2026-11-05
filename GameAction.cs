namespace CuboidFever;

// Everything the player can ask the game to do. Game code uses these, never raw keys.
public enum GameAction
{
    // Movement
    MoveForward, MoveBack, MoveLeft, MoveRight, Jump, Descend,

    // Block interaction
    BreakBlock, PlaceBlock, SelectBlock,
    // Hotbar1..Hotbar9 must stay in order: Hotbar.Update offsets from Hotbar1
    Hotbar1, Hotbar2, Hotbar3, Hotbar4, Hotbar5, Hotbar6, Hotbar7, Hotbar8, Hotbar9,

    // Toggles and debug
    ToggleFly, CycleFpsCap, CycleWindowMode, CycleLeafCulling, ToggleAo,
    PauseTime, ScrubTimeBack, ScrubTimeForward,
    ToggleDebugOverlay, CycleRenderDistance,

    // Application
    Quit,
}
