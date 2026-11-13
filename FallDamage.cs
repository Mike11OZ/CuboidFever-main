using System;

namespace CuboidFever;

// How much a fall hurts. Falling up to SafeDistance blocks is free; beyond that every extra block costs
// DamagePerBlock, so the damage grows with the height. With the numbers below a fall of 23 blocks is lethal.
public static class FallDamage
{
    public const float SafeDistance = 3f;     // blocks you can drop without a scratch
    public const float DamagePerBlock = 5f;   // health lost for each block beyond that

    public static float Calculate(float fallDistance)
    {
        if (!(fallDistance > SafeDistance)) return 0f; // also covers NaN
        return (fallDistance - SafeDistance) * DamagePerBlock;
    }
}
