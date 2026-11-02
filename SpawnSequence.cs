using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// The loading phase at the start of the game: shows the splash screen, waits for the world around the
// spawn point to generate, then places the player on the surface and hands over to gameplay.
public sealed class SpawnSequence
{
    private const float MinSplashSeconds = 2.0f;

    // World X/Z the player spawns at. The height is found by searching down for the first solid block.
    private const int SpawnX = 128, SpawnZ = 128;

    private readonly World _world;
    private readonly PlayerController _controller;
    private readonly StartupScreen _splash = new();

    private float _elapsed;

    public SpawnSequence(World world, PlayerController controller)
    {
        _world = world;
        _controller = controller;
    }

    // True once the player has been placed in the world; the game switches from splash to gameplay.
    public bool HasSpawned { get; private set; }

    // Raised once, right after the player is placed. Use it for things that start with gameplay
    // (centering the mouse, starting music).
    public event Action Spawned;

    public void LoadContent(GraphicsDevice graphicsDevice) => _splash.LoadContent(graphicsDevice);

    // Call each frame until HasSpawned is true.
    public void Update(float dt)
    {
        if (HasSpawned) return;

        _elapsed += dt;

        // Keep the splash screen up for a minimum time, and until the spawn column exists
        if (_elapsed < MinSplashSeconds) return;
        if (!_world.IsColumnLoaded(SpawnX, SpawnZ)) return;

        int y = World.Height - 1;
        while (y > 0 && !_world.IsSolid(SpawnX, y, SpawnZ)) y--;

        _controller.Spawn(new Vector3(SpawnX + 0.5f, y + 1.01f, SpawnZ + 0.5f));

        HasSpawned = true;
        Spawned?.Invoke();
    }

    public void DrawSplash(SpriteBatch sprites, GraphicsDevice graphicsDevice) =>
        _splash.Draw(sprites, graphicsDevice);
}
