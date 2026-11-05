using System;

namespace CuboidFever;

// Cave generation in the style of Minecraft Beta 1.x through 1.6.4 (MapGenCaves): long, winding "spaghetti" tunnels
// with the occasional branch and the occasional big room.
//
// Unlike the terrain, which is a density field sampled per column, caves are carved by "worms". A worm is a point
// that walks a wandering path, and at every step it removes a small ellipsoid of blocks around itself.
//
// The trick that lets this work one column at a time (columns generate independently on worker threads) is that
// every column replays the worms of ALL columns around it, not just its own:
//
//   1. For the column being generated, visit every column within Range (8) columns of it, itself included.
//   2. Seed a random generator from that neighbour's coordinates and the world seed. The same neighbour always
//      produces the same worms, whichever column is asking.
//   3. Walk each worm along its whole path, but only write the blocks that land inside the column being generated.
//
// So a tunnel that starts 5 columns away and crosses three columns is carved identically by each of them, and the
// pieces line up at the borders without the columns ever talking to each other. The random numbers a worm uses
// never depend on which column is asking, which is what makes that true. Never branch the random draws on the
// target column, or the tunnels will stop matching at the borders.
//
// Worms are only ever 112 steps long (Range * 16 - 16) and are dropped early once they can no longer reach the
// target column, so replaying the neighbours is cheap.
//
// Differences from Minecraft, on purpose:
//   - No lava: this game has none, so tunnels below y = 10 are plain air.
//   - Sand is not carved (Minecraft's rule too), so beaches stay put and cave mouths don't open on them.
//
// Thread safety: immutable after construction; Carve uses only locals, so concurrent calls are safe.
public sealed class CaveCarver
{
    // How far away, in columns, a worm can start and still reach the column being generated.
    private const int Range = 8;

    // Roughly 1 column in CaveRarity starts any caves at all; the ones that do can start up to 40 systems.
    // Raise it for fewer caves, lower it for more.
    private const int CaveRarity = 15;
    private const int MaxSystemsPerColumn = 40;

    // Systems start low: y = random(random(SpawnYSpan) + 8), so most are deep and few are near the surface.
    private const int SpawnYSpan = 120;

    // Never carve the bottom layer, or the top 8 layers of the world.
    private const int MinCarveY = 1;
    private const int MaxCarveY = World.Height - 8;

    private readonly ulong _seed;
    private readonly byte _air, _stone, _dirt, _grass;
    private readonly bool[] _isWater;

    public CaveCarver(int seed, BlockRegistry blocks)
        : this(seed,
               blocks.GetId("cuboidfever:air"),
               blocks.GetId("cuboidfever:stone"),
               blocks.GetId("cuboidfever:dirt"),
               blocks.GetId("cuboidfever:grass"),
               WaterTable(blocks))
    {
    }

    public CaveCarver(int seed, byte air, byte stone, byte dirt, byte grass, bool[] isWater)
    {
        // Mixed with a constant so the cave stream doesn't line up with other things seeded from the world seed.
        _seed = unchecked((ulong)(long)seed ^ 0xCA7E5CA7E5CA7E55UL);
        _air = air;
        _stone = stone;
        _dirt = dirt;
        _grass = grass;
        _isWater = isWater != null
            ? (bool[])isWater.Clone()
            : throw new ArgumentNullException(nameof(isWater));
    }

    private static bool[] WaterTable(BlockRegistry blocks)
    {
        var table = new bool[256];
        for (int i = 0; i < table.Length; i++)
            table[i] = blocks.Info[i].IsWater;
        return table;
    }

    // Carve caves into a freshly generated column (terrain and surface already done). column is [x, y, z].
    public void Carve(int columnX, int columnZ, byte[,,] column)
    {
        for (int sz = columnZ - Range; sz <= columnZ + Range; sz++)
        for (int sx = columnX - Range; sx <= columnX + Range; sx++)
            StartSystemsFrom(sx, sz, columnX, columnZ, column);
    }

    // Everything a source column spawns. This must draw exactly the same random numbers no matter which target
    // column is asking (see the top of the file).
    private void StartSystemsFrom(int sourceX, int sourceZ, int targetX, int targetZ, byte[,,] column)
    {
        var rng = new CaveRandom(Mix(_seed, sourceX, sourceZ));

        // Skewed hard toward small numbers: most columns get 0, a few get a handful, very rarely dozens.
        int systems = rng.Next(rng.Next(rng.Next(MaxSystemsPerColumn) + 1) + 1);
        if (rng.Next(CaveRarity) != 0) systems = 0;

        for (int i = 0; i < systems; i++)
        {
            double x = sourceX * 16 + rng.Next(16);
            double y = rng.Next(rng.Next(SpawnYSpan) + 8);
            double z = sourceZ * 16 + rng.Next(16);

            int tunnels = 1;

            // One system in four starts with a big, flattened room, plus up to 3 extra tunnels leaving it.
            if (rng.Next(4) == 0)
            {
                double roomSize = 1.0 + rng.NextDouble() * 6.0;
                CarveWorm(rng.NextSeed(), targetX, targetZ, column, x, y, z, roomSize, 0.0, 0.0, -1, -1, 0.5);
                tunnels += rng.Next(4);
            }

            for (int t = 0; t < tunnels; t++)
            {
                double yaw = rng.NextDouble() * Math.PI * 2.0;
                double pitch = (rng.NextDouble() - 0.5) * 2.0 / 8.0;   // starts nearly level
                double size = rng.NextDouble() * 2.0 + rng.NextDouble();

                // One tunnel in ten is much wider.
                if (rng.Next(10) == 0)
                    size *= rng.NextDouble() * rng.NextDouble() * 3.0 + 1.0;

                CarveWorm(rng.NextSeed(), targetX, targetZ, column, x, y, z, size, yaw, pitch, 0, 0, 1.0);
            }
        }
    }

    // Walks one worm and carves the part of its path that lies inside the target column.
    //   size   - how thick the tunnel gets at its fattest (radius is 1.5 + sin(progress) * size)
    //   yaw    - heading in the XZ plane, radians; pitch - heading up or down, radians
    //   step / endStep - where along the path to start and stop; endStep <= 0 picks a random length.
    //                    step = -1 marks a room: it starts halfway and carves just once.
    //   yScale - how squashed the carved shape is vertically (0.5 for rooms)
    private void CarveWorm(ulong seed, int targetX, int targetZ, byte[,,] column,
                           double x, double y, double z,
                           double size, double yaw, double pitch,
                           int step, int endStep, double yScale)
    {
        double centerX = targetX * 16 + 8;
        double centerZ = targetZ * 16 + 8;
        double yawChange = 0.0, pitchChange = 0.0;

        var rng = new CaveRandom(seed);

        if (endStep <= 0)
        {
            int longest = Range * 16 - 16;
            endStep = longest - rng.Next(longest / 4);
        }

        bool room = false;
        if (step == -1)
        {
            step = endStep / 2;
            room = true;
        }

        int branchAt = rng.Next(endStep / 2) + endStep / 4;
        bool steep = rng.Next(6) == 0;   // one worm in six keeps its climb or dive going for longer

        for (; step < endStep; step++)
        {
            // Thin at the ends, thickest in the middle
            double radius = 1.5 + Math.Sin(step * Math.PI / endStep) * size;
            double yRadius = radius * yScale;

            // Move one block along the heading
            double cosPitch = Math.Cos(pitch);
            x += Math.Cos(yaw) * cosPitch;
            y += Math.Sin(pitch);
            z += Math.Sin(yaw) * cosPitch;

            // The heading levels out on its own and gets randomly nudged: this is what makes it wander.
            pitch *= steep ? 0.92 : 0.7;
            pitch += pitchChange * 0.1;
            yaw += yawChange * 0.1;
            pitchChange *= 0.9;
            yawChange *= 0.75;
            pitchChange += (rng.NextDouble() - rng.NextDouble()) * rng.NextDouble() * 2.0;
            yawChange += (rng.NextDouble() - rng.NextDouble()) * rng.NextDouble() * 4.0;

            // Partway along a thick-enough worm, replace it with two thinner ones leaving at right angles.
            // The branches are under size 1, so they never branch again.
            if (!room && step == branchAt && size > 1.0)
            {
                CarveWorm(rng.NextSeed(), targetX, targetZ, column, x, y, z,
                          rng.NextDouble() * 0.5 + 0.5, yaw - Math.PI / 2.0, pitch / 3.0, step, endStep, 1.0);
                CarveWorm(rng.NextSeed(), targetX, targetZ, column, x, y, z,
                          rng.NextDouble() * 0.5 + 0.5, yaw + Math.PI / 2.0, pitch / 3.0, step, endStep, 1.0);
                return;
            }

            // Carve on only three steps in four, which leaves the little bridges and pinches seen in old caves
            if (!room && rng.Next(4) == 0) continue;

            // Give up once the worm is too far away to get back to this column in the steps it has left
            double dx = x - centerX;
            double dz = z - centerZ;
            double stepsLeft = endStep - step;
            double reach = size + 2.0 + 16.0;
            if (dx * dx + dz * dz - stepsLeft * stepsLeft > reach * reach) return;

            // Not near this column (yet)
            if (x < centerX - 16.0 - radius * 2.0 || z < centerZ - 16.0 - radius * 2.0 ||
                x > centerX + 16.0 + radius * 2.0 || z > centerZ + 16.0 + radius * 2.0)
            {
                // A room is one blob at one place. Minecraft lets it keep walking, which can carve a second,
                // shifted blob in a far column that the room's own column never made. Stop instead, so every
                // column agrees where the room is.
                if (room) return;
                continue;
            }

            TryCarve(column, targetX, targetZ, x, y, z, radius, yRadius);
            if (room) return;
        }
    }

    // Carves an ellipsoid centred on (x, y, z), clipped to the column. Returns false, carving nothing, if the area
    // touches water, so tunnels never open into the sea or a lake (they would drain it, and water now flows).
    private bool TryCarve(byte[,,] column, int targetX, int targetZ,
                          double x, double y, double z, double radius, double yRadius)
    {
        int baseX = targetX * 16;
        int baseZ = targetZ * 16;

        // The box around the ellipsoid, in column coordinates, with a block of margin, clipped to the column
        int minX = Math.Max((int)Math.Floor(x - radius) - baseX - 1, 0);
        int maxX = Math.Min((int)Math.Floor(x + radius) - baseX + 1, 16);
        int minY = Math.Max((int)Math.Floor(y - yRadius) - 1, MinCarveY);
        int maxY = Math.Min((int)Math.Floor(y + yRadius) + 1, MaxCarveY);
        int minZ = Math.Max((int)Math.Floor(z - radius) - baseZ - 1, 0);
        int maxZ = Math.Min((int)Math.Floor(z + radius) - baseZ + 1, 16);

        if (minX >= maxX || minY >= maxY || minZ >= maxZ) return true;

        if (TouchesWater(column, minX, maxX, minY, maxY, minZ, maxZ)) return false;

        for (int lx = minX; lx < maxX; lx++)
        {
            double xd = (lx + baseX + 0.5 - x) / radius;

            for (int lz = minZ; lz < maxZ; lz++)
            {
                double zd = (lz + baseZ + 0.5 - z) / radius;
                if (xd * xd + zd * zd >= 1.0) continue;

                // Top to bottom, so we know whether grass was removed above a dirt block we reach next
                bool grassAbove = false;

                for (int ly = maxY - 1; ly >= minY; ly--)
                {
                    double yd = (ly + 0.5 - y) / yRadius;

                    // yd > -0.7 cuts off the bottom of the ellipsoid, so tunnel floors are flat-ish
                    if (yd <= -0.7 || xd * xd + yd * yd + zd * zd >= 1.0) continue;

                    byte b = column[lx, ly, lz];
                    if (b == _grass) grassAbove = true;

                    if (b != _stone && b != _dirt && b != _grass) continue;

                    column[lx, ly, lz] = _air;

                    // A hole through the surface: the dirt now exposed at its bottom becomes grass, as if it
                    // had always been the top
                    if (grassAbove && ly > 0 && column[lx, ly - 1, lz] == _dirt)
                        column[lx, ly - 1, lz] = _grass;
                }
            }
        }

        return true;
    }

    // Is there water in the box? Only the outer shell is checked, the inside is skipped: any water in there
    // reaches out to the shell, and the shell is much smaller.
    private bool TouchesWater(byte[,,] column, int minX, int maxX, int minY, int maxY, int minZ, int maxZ)
    {
        int top = Math.Min(maxY + 1, World.Height - 1);
        int bottom = Math.Max(minY - 1, 0);

        for (int lx = minX; lx < maxX; lx++)
        for (int lz = minZ; lz < maxZ; lz++)
        {
            bool onEdge = lx == minX || lx == maxX - 1 || lz == minZ || lz == maxZ - 1;

            if (onEdge)
            {
                for (int ly = top; ly >= bottom; ly--)
                    if (_isWater[column[lx, ly, lz]]) return true;
            }
            else
            {
                // Inside: just the top and bottom caps
                if (_isWater[column[lx, top, lz]] || _isWater[column[lx, bottom, lz]]) return true;
            }
        }

        return false;
    }

    // Hash of (seed, column x, column z) into a well-spread 64-bit number
    private static ulong Mix(ulong seed, int x, int z)
    {
        unchecked
        {
            ulong h = seed + (ulong)(long)x * 0x9E3779B97F4A7C15UL;
            h = Scramble(h);
            h += (ulong)(long)z * 0xC2B2AE3D27D4EB4FUL;
            return Scramble(h);
        }
    }

    private static ulong Scramble(ulong z)
    {
        unchecked
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    // SplitMix64. Small, fast, and identical on every machine and .NET version, which System.Random does not promise.
    private sealed class CaveRandom
    {
        private ulong _state;

        public CaveRandom(ulong seed) => _state = seed;

        private ulong NextBits()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                return Scramble(_state);
            }
        }

        // A seed for a new CaveRandom (a worm's own random stream)
        public ulong NextSeed() => NextBits();

        // 0 up to (but not including) bound; bound must be above 0
        public int Next(int bound) => (int)(NextBits() % (ulong)bound);

        // 0 up to (but not including) 1
        public double NextDouble() => (NextBits() >> 11) * (1.0 / 9007199254740992.0);
    }
}