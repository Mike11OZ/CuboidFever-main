using System;

namespace CuboidFever;

// Says "now" once every so many seconds of game time. Used for autosaving, so the timing can be tested.
public sealed class IntervalTimer
{
    private readonly float _interval;
    private float _elapsed;

    public IntervalTimer(float intervalSeconds)
    {
        if (intervalSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
        _interval = intervalSeconds;
    }

    // Adds the time since the last frame. True when the interval has passed; the count then starts again from zero,
    // so one very long frame (a stall) gives one "now", not a burst of them.
    public bool Advance(float deltaSeconds)
    {
        _elapsed += Math.Max(0f, deltaSeconds);
        if (_elapsed < _interval) return false;

        _elapsed = 0f;
        return true;
    }

    // Starts counting from zero again (a new world was opened).
    public void Reset() => _elapsed = 0f;
}
