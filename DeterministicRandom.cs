using System;

namespace CuboidFever;

/// <summary>
/// Small deterministic pseudo-random number generator based on SplitMix64.
///
/// Produces identical sequences across .NET versions and platforms.
/// Each instance owns its own state and is safe to use concurrently
/// only when that instance itself is not shared between threads.
///
/// Create separate instances for independent generation operations.
/// </summary>
public sealed class DeterministicRandom
{
    private ulong _state;

    public DeterministicRandom(ulong seed)
    {
        _state = seed;
    }

    /// <summary>
    /// Creates a deterministic random stream for a particular feature
    /// in a particular world column.
    /// </summary>
    public static DeterministicRandom ForFeature(
        int worldSeed,
        int featureSalt,
        int columnX,
        int columnZ)
    {
        unchecked
        {
            ulong seed = (uint)worldSeed;

            seed = Scramble(
                seed ^ ((ulong)(uint)featureSalt * 0x9E3779B97F4A7C15UL));

            seed = Scramble(
                seed ^ ((ulong)(long)columnX * 0xC2B2AE3D27D4EB4FUL));

            seed = Scramble(
                seed ^ ((ulong)(long)columnZ * 0x165667B19E3779F9UL));

            return new DeterministicRandom(seed);
        }
    }

    /// <summary>
    /// Generates the next 64 random bits.
    /// </summary>
    private ulong NextBits()
    {
        unchecked
        {
            _state += 0x9E3779B97F4A7C15UL;
            return Scramble(_state);
        }
    }

    /// <summary>
    /// SplitMix64 mixing function.
    /// </summary>
    private static ulong Scramble(ulong value)
    {
        unchecked
        {
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }

    /// <summary>
    /// Returns a non-negative integer.
    /// </summary>
    public int Next()
    {
        return (int)(NextBits() & 0x7FFFFFFF);
    }

    /// <summary>
    /// Returns an integer from 0 (inclusive) to maxValue (exclusive).
    /// </summary>
    public int Next(int maxValue)
    {
        if (maxValue <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxValue));

        return (int)(NextBits() % (uint)maxValue);
    }

    /// <summary>
    /// Returns an integer from minValue (inclusive) to maxValue (exclusive).
    /// </summary>
    public int Next(int minValue, int maxValue)
    {
        if (minValue > maxValue)
            throw new ArgumentOutOfRangeException(nameof(minValue));

        if (minValue == maxValue)
            return minValue;

        ulong range = (ulong)((long)maxValue - minValue);
        ulong value = NextBits() % range;

        return (int)(minValue + (long)value);
    }

    /// <summary>
    /// Returns a double greater than or equal to 0.0 and less than 1.0.
    /// </summary>
    public double NextDouble()
    {
        return (NextBits() >> 11) * (1.0 / 9007199254740992.0);
    }
}