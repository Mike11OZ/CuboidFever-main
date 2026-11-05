using System;
using Microsoft.Xna.Framework;

namespace CuboidFever;

/// <summary>
/// Beta-style climate: two big, slowly varying 2D noise fields (temperature and
/// humidity) are sampled per block column and looked up in a small table to
/// pick a biome. See Beta 1.7's WorldChunkManager + BiomeGenBase.getBiome.
///
/// Temperature and humidity both end up in 0..1. Humidity is multiplied by
/// temperature in the lookup, so cold places are always dry.
///
/// Uses its own noise instances seeded from the world seed, NOT the
/// TerrainGenerator's Random, so adding this doesn't change the terrain shape
/// of any existing seed.
///
/// Thread safety: noise sampling is read-only, so this is safe to call from
/// the worker threads and the main thread at once.
/// </summary>
public sealed class ClimateMap
{
    // Same per-block frequencies Beta used (temperature changes slower than humidity).
    private const double TemperatureScale = 0.025;
    private const double HumidityScale = 0.05;
    private const double DetailScale = 0.25;

    private const int TableSize = 64;

    private readonly OctaveNoise _temperatureNoise;
    private readonly OctaveNoise _humidityNoise;
    private readonly OctaveNoise _detailNoise;

    private readonly Biomes _biomes;
    private readonly Biome[] _lookup = new Biome[TableSize * TableSize];

    public ClimateMap(int seed, BlockRegistry blocks)
    {
        _biomes = new Biomes(blocks);

        // Different multipliers keep the three fields uncorrelated (same trick as Beta).
        _temperatureNoise = new OctaveNoise(new Random(unchecked(seed * 9871)), 4);
        _humidityNoise = new OctaveNoise(new Random(unchecked(seed * 39811)), 4);
        _detailNoise = new OctaveNoise(new Random(unchecked(seed * 543321)), 2);

        for (int t = 0; t < TableSize; t++)
        for (int h = 0; h < TableSize; h++)
            _lookup[t + h * TableSize] = PickBiome(t / (double)(TableSize - 1), h / (double)(TableSize - 1));
    }

    /// <summary>Temperature and humidity (both 0..1) at a world block column.</summary>
    public (double temperature, double humidity) Sample(int wx, int wz)
    {
        double temp = _temperatureNoise.Sample(wx * TemperatureScale, wz * TemperatureScale, 0);
        double humid = _humidityNoise.Sample(wx * HumidityScale, wz * HumidityScale, 0);

        // A little fine-grained wobble so biome borders aren't perfectly smooth contours.
        double detail = _detailNoise.Sample(wx * DetailScale, wz * DetailScale, 0) * 1.1 + 0.5;

        temp = (temp * 0.15 + 0.7) * 0.99 + detail * 0.01;
        humid = (humid * 0.15 + 0.5) * 0.998 + detail * 0.002;

        // Pushes temperature toward 1 so most of the world is temperate/warm.
        temp = 1.0 - (1.0 - temp) * (1.0 - temp);

        return (Math.Clamp(temp, 0, 1), Math.Clamp(humid, 0, 1));
    }

    /// <summary>
    /// How much the local climate allows rugged terrain, 0..1 (Beta's
    /// "var25"). Wet and warm gives close to 1; cold or dry gives low values,
    /// which flattens the land. Roughly: tundra 0.3-0.4, desert/savanna
    /// 0.4-0.6, temperate forest 0.9+.
    /// </summary>
    public static double Hilliness(double temperature, double humidity)
    {
        double f = 1.0 - temperature * humidity;
        f *= f;
        f *= f; // f = (1 - t*h)^4, close to 1 when t*h is small (cold/dry)
        return 1.0 - f;
    }

    public double Hilliness(int wx, int wz)
    {
        var (temp, humid) = Sample(wx, wz);
        return Hilliness(temp, humid);
    }

    // ---- Vegetation tint ----
    private static readonly Vector3 GrassDry = new(1.00f, 0.72f, 0.42f);
    private static readonly Vector3 GrassCold = new(0.72f, 0.88f, 0.85f);
    private static readonly Vector3 FoliageDry = new(1.00f, 0.80f, 0.55f);
    private static readonly Vector3 FoliageCold = new(0.75f, 0.90f, 0.90f);

    private const double LushWetness = 0.5;
    private const double ColdStart = 0.6;
    private const double ColdRange = 0.5;

    /// <summary>
    /// Grass and foliage tint multipliers at a world block column. Computed
    /// continuously from temperature and humidity (not from the biome table), so
    /// colours fade across biome borders instead of changing in a hard line.
    /// </summary>
    public void GetTints(int wx, int wz, out Vector3 grass, out Vector3 foliage)
    {
        var (temp, humid) = Sample(wx, wz);

        float dry = (float)Math.Clamp(1.0 - temp * humid / LushWetness, 0, 1);
        float cold = (float)Math.Clamp((ColdStart - temp) / ColdRange, 0, 1);

        grass = Vector3.Lerp(Vector3.Lerp(Vector3.One, GrassDry, dry), GrassCold, cold);
        foliage = Vector3.Lerp(Vector3.Lerp(Vector3.One, FoliageDry, dry), FoliageCold, cold);
    }

    public Biome GetBiome(int wx, int wz)
    {
        var (temp, humid) = Sample(wx, wz);
        return _lookup[(int)(temp * 63) + (int)(humid * 63) * TableSize];
    }

    // Beta's biome chart. "humid" is scaled by temperature first, so the
    // thresholds below are really thresholds on (temperature * humidity).
    private Biome PickBiome(double temp, double humid)
    {
        humid *= temp;

        if (temp < 0.1) return _biomes.Tundra;

        if (humid < 0.2)
        {
            if (temp < 0.5) return _biomes.Tundra;
            return temp < 0.95 ? _biomes.Savanna : _biomes.Desert;
        }

        if (humid > 0.5 && temp < 0.7) return _biomes.Swampland;
        if (temp < 0.5) return _biomes.Taiga;

        if (temp < 0.97)
            return humid < 0.35 ? _biomes.Shrubland : _biomes.Forest;

        if (humid < 0.45) return _biomes.Plains;
        return humid < 0.9 ? _biomes.SeasonalForest : _biomes.Rainforest;
    }
}