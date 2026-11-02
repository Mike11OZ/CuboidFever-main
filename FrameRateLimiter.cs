using System;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// Owns the FPS cap setting and applies it to the Game's timing.
public sealed class FrameRateLimiter
{
    private static readonly int[] Caps = { 29, 59, 71, 119, 143, 0 }; // 0 = uncapped

    private readonly Game _game;
    private int _index = 4; // start at 144

    public FrameRateLimiter(Game game) => _game = game;

    // 0 means uncapped.
    public int CurrentCap => Caps[_index];

    public void CycleNext()
    {
        _index = (_index + 1) % Caps.Length;
        Apply();
    }

    public void Apply()
    {
        int cap = CurrentCap;
        if (cap == 0)
        {
            _game.IsFixedTimeStep = false;
        }
        else
        {
            _game.IsFixedTimeStep = true;
            _game.TargetElapsedTime = TimeSpan.FromSeconds(1.0 / cap);
        }
    }
}
