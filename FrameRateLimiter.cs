using System;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// Owns the FPS cap setting and applies it to the Game's timing.
public sealed class FrameRateLimiter
{
    private static readonly int[] Caps = { 30, 60, 120, 0 }; // 0 = uncapped

    private readonly Game _game;
    private int _index = 1; // start at 60

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
