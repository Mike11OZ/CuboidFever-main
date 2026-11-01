namespace CuboidFever;

/// <summary>
/// One procedural world feature (a tree, a rock, a fallen log, grass...).
///
/// A feature is a pure function of its <see cref="FeatureContext"/>: it scatters
/// itself over one freshly generated column using the context's RNG and writes
/// blocks back into the context's column array.
///
/// Rules for implementations:
///  - Stateless after construction. Columns are generated on worker threads, so
///    one feature instance is called concurrently. Keep per-column state in locals.
///  - Resolve block IDs once, in the constructor.
///  - Draw all randomness from <see cref="FeatureContext.Rng"/>, never a shared Random.
///  - Stay inside the column. Anything that must cross column borders belongs to
///    the (future) structure system, not here.
/// </summary>
public abstract class Feature
{
    /// <summary>Stable identifier, e.g. "oak_tree". Used for diagnostics.</summary>
    public abstract string Name { get; }

    /// <summary>
    /// Distinguishes this feature's random stream from every other feature's.
    /// Must be unique across all registered features and must never change once
    /// worlds exist, or that feature's placement will shift in saved worlds.
    /// </summary>
    public abstract int Salt { get; }

    /// <summary>Place this feature into <paramref name="ctx"/>'s column.</summary>
    public abstract void Generate(FeatureContext ctx);
}
