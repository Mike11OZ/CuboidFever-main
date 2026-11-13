using System;

namespace CuboidFever;

// The player's health, from 0 to Max. Anything that hurts or heals the player goes through here.
public sealed class PlayerHealth
{
    public const float Max = 100f;

    public float Current { get; private set; } = Max;

    public bool IsDead => Current <= 0f;

    // 0 (dead) to 1 (full), for drawing the bar.
    public float Fraction => Current / Max;

    // Amounts that are not positive numbers (zero, negative, NaN) are ignored.
    public void Damage(float amount)
    {
        if (!(amount > 0f)) return;
        Current = MathF.Max(0f, Current - amount);
    }

    public void Heal(float amount)
    {
        if (!(amount > 0f)) return;
        Current = MathF.Min(Max, Current + amount);
    }

    public void Restore() => Current = Max;
}
