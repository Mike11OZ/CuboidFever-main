using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace CuboidFever;

public class DebugOverlay
{
    private bool _isVisible = false;
    private bool _f3WasDown;

    private readonly SpriteFont _font;
    private int _frames;
    private double _lastFpsTime;
    private int _currentFps;

    public bool IsVisible => _isVisible;

    public DebugOverlay(SpriteFont font)
    {
        _font = font;
    }

    public void HandleInput(KeyboardState kb)
    {
        bool f3Down = kb.IsKeyDown(Keys.F3);
        if (f3Down && !_f3WasDown)
        {
            _isVisible = !_isVisible;
        }
        _f3WasDown = f3Down;
    }

    public void Update(GameTime gameTime)
    {
        _frames++;
        if (gameTime.TotalGameTime.TotalSeconds - _lastFpsTime >= 1.0)
        {
            _currentFps = _frames;
            _frames = 0;
            _lastFpsTime = gameTime.TotalGameTime.TotalSeconds;
        }
    }

    public void Draw(SpriteBatch sprites, World world, Player player, int fpsCap, int slot, string[] hotbarNames, WindowManager windowManager)
    {
        if (!_isVisible || _font == null) return;

        int cap = fpsCap;
        string fpsStr = cap == 0 ? "none" : cap.ToString();
        
        string debugText = 
            $"{GameInfo.FullTitle}\n" +
            $"Display: {windowManager.CurrentMode} ({windowManager.Width}x{windowManager.Height})\n" +
            $"FPS: {_currentFps} (cap: {fpsStr})\n" +
            $"Leaves: {ChunkMesher.LeafCullDepth} | AO: {(ChunkMesher.AmbientOcclusion ? "on" : "off")}\n" +
            $"Chunks: {world.VisibleChunks}/{world.TotalChunks} | Queue: {world.PendingWork}\n" +
            $"XYZ: {player.Position.X:F2} / {player.Position.Y:F2} / {player.Position.Z:F2}\n" +
            $"Holding: {hotbarNames[slot]}";

        sprites.Begin(samplerState: SamplerState.PointClamp);
        // Retro drop shadow effect for clean readability
        sprites.DrawString(_font, debugText, new Vector2(11, 11), Color.Black);
        sprites.DrawString(_font, debugText, new Vector2(10, 10), Color.White);
        sprites.End();
    }
}