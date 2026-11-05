
using System;

namespace CuboidFever;

public class TerrainSurfaceGenerator
{
    private readonly int _seed;

    private readonly byte _airId;
    private readonly byte _stoneId;
    private readonly byte _waterId;
    private readonly byte _sandId;
    private readonly byte _bedrockId;

    private readonly OctaveNoise _sandNoise;
    private readonly OctaveNoise _stoneDepth;

    private readonly ClimateMap _climate;

    public TerrainSurfaceGenerator(
        int seed,
        byte airId,
        byte stoneId,
        byte waterId,
        byte sandId,
        byte bedrockId,
        OctaveNoise sandNoise,
        OctaveNoise stoneDepth,
        ClimateMap climate)
    {
        _seed = seed;

        _airId = airId;
        _stoneId = stoneId;
        _waterId = waterId;
        _sandId = sandId;
        _bedrockId = bedrockId;

        _sandNoise = sandNoise;
        _stoneDepth = stoneDepth;

        _climate = climate;
    }

    public void Generate(int cx, int cz, byte[,,] col)
    {
        var rng = new Random(
            unchecked(_seed * 31 +
            cx * 341873128 +
            cz * 132897987));

        const double d = 0.03125;

        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
        {
            int wx = cx * 16 + x;
            int wz = cz * 16 + z;

            bool isSand =
                _sandNoise.Sample(wx * d, wz * d, 0) +
                rng.NextDouble() * 0.2 > 0;

            int stoneDepth = (int)(
                _stoneDepth.Sample(
                    wx * d * 2,
                    wz * d * 2,
                    d * 2) / 3.0 +
                3.0 +
                rng.NextDouble() * 0.25);

            var biome = _climate.GetBiome(wx, wz);

            int run = -1;
            byte filler = biome.FillerBlock;

            for (int y = World.Height - 1; y >= 0; y--)
            {
                if (y < 5 && y <= rng.Next(5))
                {
                    col[x, y, z] = _bedrockId;
                    continue;
                }

                byte b = col[x, y, z];

                if (b == _airId)
                {
                    run = -1;
                    continue;
                }

                // Leave water unchanged.
                if (b != _stoneId)
                    continue;

                // First stone below air or water is the surface.
                if (run == -1)
                {
                    byte top = biome.TopBlock;
                    filler = biome.FillerBlock;

                    if (stoneDepth <= 0)
                    {
                        top = _airId;
                        filler = _stoneId;
                    }
                    else if (
                        y >= World.SeaLevel - 4 &&
                        y <= World.SeaLevel + 1 &&
                        isSand)
                    {
                        top = _sandId;
                        filler = _sandId;
                    }

                    if (y < World.SeaLevel && top == _airId)
                        top = _waterId;

                    run = stoneDepth;

                    // Above the waterline: top block.
                    // Underwater: filler, so no underwater grass.
                    col[x, y, z] =
                        y >= World.SeaLevel - 1
                            ? top
                            : filler;
                }
                else if (run > 0)
                {
                    run--;
                    col[x, y, z] = filler;
                }
            }
        }
    }
}
