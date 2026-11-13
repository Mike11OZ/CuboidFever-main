namespace CuboidFever;

public enum GameAction
{
    // Movement
    MoveForward, MoveBack, MoveLeft, MoveRight, Jump, Descend,

    // Block interaction
    BreakBlock, PlaceBlock, SelectBlock,
    Hotbar1, Hotbar2, Hotbar3, Hotbar4, Hotbar5, Hotbar6, Hotbar7, Hotbar8, Hotbar9,

    // Toggles and debug
    ToggleFly, ToggleHud, CycleFpsCap, CycleWindowMode, CycleLeafCulling, ToggleAo,
    PauseTime, ScrubTimeBack, ScrubTimeForward,
    ToggleDebugOverlay, CycleRenderDistance, Screenshot, ScreenshotPanoramic,
    OpenConsole, OpenCommandConsole, // T opens the console empty, / opens it with a slash already typed

    // Application
    Pause,
}