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

    // Where the player (feet) was when the world was last saved, or null to spawn at the default spot
    private readonly Vector3? _savedPosition;

    private const float PlayerHeight = 1.8f;

    public SpawnSequence(World world, PlayerController controller, LoadingScreen loadingScreen,
                         Vector3? savedPosition = null)
    {
        _world = world;
        _controller = controller;
        _loadingScreen = loadingScreen;
        _savedPosition = savedPosition;
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

        // Which column to wait for: the one under the saved position if there is one, otherwise the default spot
        int spotX = _savedPosition.HasValue ? (int)MathF.Floor(_savedPosition.Value.X) : SpawnX;
        int spotZ = _savedPosition.HasValue ? (int)MathF.Floor(_savedPosition.Value.Z) : SpawnZ;

        // Keep the splash screen up only until that column exists
        if (!_world.IsColumnLoaded(spotX, spotZ)) return;

        _controller.Spawn(ChooseSpot(spotX, spotZ));

        HasSpawned = true;
        Spawned?.Invoke();
    }

    // The saved position if the player still fits there, otherwise on top of the ground at the same X and Z.
    // (The world can change under a save: terrain generation updated, or a block removed from the game.)
    private Vector3 ChooseSpot(int x, int z)
    {
        if (_savedPosition is Vector3 saved)
        {
            int feetY = (int)MathF.Floor(saved.Y);
            int headY = (int)MathF.Floor(saved.Y + PlayerHeight);

            bool blocked = false;
            for (int by = feetY; by <= headY && !blocked; by++)
                blocked = _world.IsSolid(x, by, z);

            if (!blocked) return saved;
        }

        // The default spot, or a saved spot that is blocked: search down from the top for the ground
        int y = World.Height - 1;
        while (y > 0 && !_world.IsSolid(x, y, z)) y--;

        return new Vector3(x + 0.5f, y + 1.01f, z + 0.5f);
    }

    // No progress bar: we don't know how long the world around the spawn will take to generate.
    public void DrawSplash(SpriteBatch sprites, GraphicsDevice graphicsDevice) =>
        _loadingScreen.Draw(sprites, graphicsDevice);
}