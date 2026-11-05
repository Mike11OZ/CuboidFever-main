
using System;

namespace CuboidFever;

public class TerrainGenerator
{
    // Block IDs loaded from the data-driven block registry.
    private readonly byte _airId;
    private readonly byte _stoneId;
    private readonly byte _waterId;

    private const int TerrainBuildHeightBuffer = 16;

    private readonly TerrainDensityGenerator _densityGenerator;
    private readonly TerrainSurfaceGenerator _surfaceGenerator;

    /// <summary>Temperature/humidity maps and the biome lookup built on them.</summary>
    public ClimateMap Climate { get; }

    public TerrainGenerator(int seed)
    {
        // BlockRegistry must be initialized before creating the world.
        var blocks = BlockRegistry.Instance;

        _airId = blocks.GetId("cuboidfever:air");
        _stoneId = blocks.GetId("cuboidfever:stone");
        _waterId = blocks.GetId("cuboidfever:water");

        // Preserve the original noise initialization order.
        // Changing this order changes the generated terrain.
        var r = new Random(seed);

        var low = new OctaveNoise(r, 16);
        var high = new OctaveNoise(r, 16);
        var select = new OctaveNoise(r, 8);

        var sandNoise = new OctaveNoise(r, 4);
        var stoneDepth = new OctaveNoise(r, 4);

        var scale2D = new OctaveNoise(r, 10);
        var depth2D = new OctaveNoise(r, 16);
        var continentalNoise = new OctaveNoise(r, 4);

        // Climate is constructed after the noise generators.
        Climate = new ClimateMap(seed, blocks);

        _densityGenerator = new TerrainDensityGenerator(
            Climate,
            low,
            high,
            select,
            scale2D,
            depth2D,
            continentalNoise);

        _surfaceGenerator = new TerrainSurfaceGenerator(
            seed,
            _airId,
            _stoneId,
            _waterId,
            blocks.GetId("cuboidfever:sand"),
            blocks.GetId("cuboidfever:dragon_stone"),
            sandNoise,
            stoneDepth,
            Climate);
    }

    public void GenerateColumn(int cx, int cz, byte[,,] col)
    {
        ArgumentNullException.ThrowIfNull(col);

        if (col.GetLength(0) != 16 ||
            col.GetLength(1) != World.Height ||
            col.GetLength(2) != 16)
        {
            throw new ArgumentException(
                "Column dimensions do not match world dimensions.",
                nameof(col));
        }

        var density = _densityGenerator.Generate(cx, cz);

        FillFromDensity(density, col);
        _surfaceGenerator.Generate(cx, cz, col);
    }

    private void FillFromDensity(double[,,] g, byte[,,] col)
    {
        int maxTerrainY = World.Height - TerrainBuildHeightBuffer;

        for (int i = 0; i < 4; i++)
        for (int k = 0; k < 4; k++)
        for (int j = 0; j < 16; j++)
        {
            for (int dy = 0; dy < 8; dy++)
            {
                double fy = dy / 8.0;

                double a = Lerp(
                    fy, g[i, j, k], g[i, j + 1, k]);

                double b = Lerp(
                    fy, g[i + 1, j, k], g[i + 1, j + 1, k]);

                double c = Lerp(
                    fy, g[i, j, k + 1], g[i, j + 1, k + 1]);

                double e = Lerp(
                    fy, g[i + 1, j, k + 1],
                    g[i + 1, j + 1, k + 1]);

                int y = j * 8 + dy;

                for (int dx = 0; dx < 4; dx++)
                {
                    double fx = dx / 4.0;

                    double ab = Lerp(fx, a, b);
                    double ce = Lerp(fx, c, e);

                    for (int dz = 0; dz < 4; dz++)
                    {
                        double v = Lerp(dz / 4.0, ab, ce);

                        byte block = v > 0
                            ? _stoneId
                            : y < World.SeaLevel
                                ? _waterId
                                : _airId;

                        // Absolute safety cap: no terrain or water above this elevation.
                        if (y >= maxTerrainY)
                            block = _airId;

                        col[
                            i * 4 + dx,
                            y,
                            k * 4 + dz] = block;
                    }
                }
            }
        }
    }

    private static double Lerp(
        double t,
        double a,
        double b)
        => a + t * (b - a);
}
