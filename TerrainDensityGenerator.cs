
using System;

namespace CuboidFever;

public class TerrainDensityGenerator
{
    // ---- Tunables ----
    private const double BaseHeight = 0.2;
    private const double DepthInfluence = 2.0;
    private const double Roughness = 1.0;

    // Keep at least 16 blocks of vertical building space above terrain.
    private const int TerrainBuildHeightBuffer = 16;

    // Begin reducing terrain elevations this far below the hard cap.
    private const int HighAltitudeFadeDepth = 8;

    // Continentalness configuration.
    private const double ContinentalNoiseScale = 600.0;
    private const double ContinentalNormalize = 5.0;
    private const double ContinentalHeightInfluence = 0.7;

    // How much normal terrain roughness the coldest/driest climates keep.
    private const double ClimateFlatFloor = 0.5;

    private const double NoiseScale = 684.412;
    private const int GX = 5, GY = 17, GZ = 5;

    private readonly OctaveNoise _low;
    private readonly OctaveNoise _high;
    private readonly OctaveNoise _select;
    private readonly OctaveNoise _scale2D;
    private readonly OctaveNoise _depth2D;
    private readonly OctaveNoise _continentalNoise;

    private readonly ClimateMap _climate;

    public TerrainDensityGenerator(
        ClimateMap climate,
        OctaveNoise low,
        OctaveNoise high,
        OctaveNoise select,
        OctaveNoise scale2D,
        OctaveNoise depth2D,
        OctaveNoise continentalNoise)
    {
        ArgumentNullException.ThrowIfNull(climate);
        ArgumentNullException.ThrowIfNull(low);
        ArgumentNullException.ThrowIfNull(high);
        ArgumentNullException.ThrowIfNull(select);
        ArgumentNullException.ThrowIfNull(scale2D);
        ArgumentNullException.ThrowIfNull(depth2D);
        ArgumentNullException.ThrowIfNull(continentalNoise);

        _climate = climate;

        _low = low;
        _high = high;
        _select = select;
        _scale2D = scale2D;
        _depth2D = depth2D;
        _continentalNoise = continentalNoise;
    }

    public double[,,] Generate(int cx, int cz)
    {
        var density = new double[GX, GY, GZ];

        int maxTerrainY = World.Height - TerrainBuildHeightBuffer;
        int fadeStartY = maxTerrainY - HighAltitudeFadeDepth;

        for (int i = 0; i < GX; i++)
        for (int k = 0; k < GZ; k++)
        {
            // Global lattice coordinates ensure neighboring chunks
            // share identical edge samples.
            double cellX = cx * 4 + i;
            double cellZ = cz * 4 + k;

            // Precompute coordinates shared by the 3D noise samples.
            double noiseX = cellX * NoiseScale;
            double noiseZ = cellZ * NoiseScale;

            // 2D noise: overall height and roughness.
            double scaleN = _scale2D.Sample(
                cellX * 1.121, 10, cellZ * 1.121);

            double depthN = ShapeDepth(
                _depth2D.Sample(
                    cellX * 200, 10, cellZ * 200) / 8000.0);

            // Macro-scale continentalness.
            double continentalness = _continentalNoise.Sample(
                cellX * 4 / ContinentalNoiseScale,
                0,
                cellZ * 4 / ContinentalNoiseScale) / ContinentalNormalize;

            // Original roughness term.
            double baseVariation =
                Math.Clamp(
                    (scaleN + 256) / 512 * Roughness,
                    0, 1) * 0.9 + 0.1;

            // Cold and dry regions get flatter terrain.
            double hilliness = _climate.Hilliness(
                (int)(cellX * 4),
                (int)(cellZ * 4));

            double variation = baseVariation *
                Lerp(hilliness, ClimateFlatFloor, 1.0);

            // Height offset incorporates continental noise.
            double heightOffset =
                BaseHeight
                + (continentalness * ContinentalHeightInfluence)
                + (depthN * DepthInfluence);

            double centerCell =
                GY / 2.0 + heightOffset * (GY / 16.0) * 4.0;

            // Convert target terrain elevation to block coordinates.
            double targetHeight = centerCell * 8.0;

            // Compress high elevations smoothly toward the build height limit.
            if (targetHeight > fadeStartY)
            {
                double excessHeight = targetHeight - fadeStartY;

                targetHeight = fadeStartY +
                    HighAltitudeFadeDepth *
                    (1.0 - Math.Exp(
                        -excessHeight / HighAltitudeFadeDepth));
            }

            centerCell = targetHeight / 8.0;

            for (int j = 0; j < GY; j++)
            {
                double lo = _low.Sample(
                    noiseX,
                    j * NoiseScale,
                    noiseZ) / 512;

                double hi = _high.Sample(
                    noiseX,
                    j * NoiseScale,
                    noiseZ) / 512;

                double sel = (
                    _select.Sample(
                        noiseX / 80,
                        j * NoiseScale / 160,
                        noiseZ / 80) / 10 + 1) / 2;

                double v = sel < 0
                    ? lo
                    : sel > 1
                        ? hi
                        : lo + (hi - lo) * sel;

                // Density falls as altitude increases.
                // Below the center, the gradient is four times stronger.
                double grad = (j - centerCell) * 12.0 / variation;

                if (grad < 0)
                    grad *= 4;

                v -= grad;

                // Force air at the very top of the world.
                if (j > GY - 4)
                {
                    double t = (j - (GY - 4)) / 3.0;
                    v = v * (1 - t) + -10 * t;
                }

                density[i, j, k] = v;
            }
        }

        return density;
    }

    private static double ShapeDepth(double d)
    {
        if (d < 0)
            d = -d * 0.3;

        d = d * 3 - 2;

        if (d < 0)
        {
            d /= 2;

            if (d < -1)
                d = -1;

            d /= 1.4;
            d /= 2;
        }
        else
        {
            if (d > 1)
                d = 1;

            d /= 8;
        }

        return d;
    }

    private static double Lerp(double t, double a, double b)
        => a + t * (b - a);
}
