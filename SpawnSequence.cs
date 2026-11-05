using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// The loading phase between startup and gameplay: shows a loading screen, waits for the world around the
// spawn point to generate, then places the player on the surface and hands over to gameplay.
public sealed class SpawnSequence
{
    // World X/Z the player spawns at. The height is found by searching down for the first solid block.
    private const int SpawnX = 128, SpawnZ = 128;

    private readonly World _world;
    private readonly PlayerController _controller;
    private readonly LoadingScreen _loadingScreen;

    public SpawnSequence(World world, PlayerController controller, LoadingScreen loadingScreen)
    {
        _world = world;
        _controller = controller;
        _loadingScreen = loadingScreen;
    }

    // True once the player has been placed in the world; the game switches from splash to gameplay.
    public bool HasSpawned { get; private set; }

    // Raised once, right after the player is placed. Use it for things that start with gameplay
    // (centering the mouse, starting music).
    public event Action Spawned;

    // Call each frame until HasSpawned is true.
    public void Update(float dt)
    {
        if (HasSpawned) return;

        // Keep the splash screen up only until the spawn column exists
        if (!_world.IsColumnLoaded(SpawnX, SpawnZ)) return;

        int y = World.Height - 1;
        while (y > 0 && !_world.IsSolid(SpawnX, y, SpawnZ)) y--;

        _controller.Spawn(new Vector3(SpawnX + 0.5f, y + 1.01f, SpawnZ + 0.5f));

        HasSpawned = true;
        Spawned?.Invoke();
    }

    // No progress bar: we don't know how long the world around the spawn will take to generate.
    public void DrawSplash(SpriteBatch sprites, GraphicsDevice graphicsDevice) =>
        _loadingScreen.Draw(sprites, graphicsDevice);
}