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

    public void Draw(SpriteBatch sprites, World world, Player player, int fpsCap, int slot, string[] hotbarNames,
                    WindowManager windowManager, DayNightCycle dayNight,
                    bool hasTarget, (int x, int y, int z) target, (int x, int y, int z) targetPrev)
    {
        if (!_isVisible || _font == null) return;

        int cap = fpsCap;
        string fpsStr = cap == 0 ? "none" : cap.ToString();

        string debugText =
            $"{GameInfo.FullTitle}\n" +
            $"Display: {windowManager.CurrentMode} ({windowManager.Width}x{windowManager.Height})\n" +
            $"FPS: {_currentFps} (cap: {fpsStr})\n" +
            $"Leaves: {ChunkMesher.LeafCullDepth} | AO: {(ChunkMesher.AmbientOcclusion ? "on" : "off")}\n" +
            $"Time: {dayNight.TimeString} | Day: {dayNight.DayLengthSeconds:F0}s | Sky darkness: -{dayNight.SkyDarknessLevels}{(dayNight.Paused ? " | PAUSED" : "")}\n" +
            $"Chunks: {world.VisibleChunks}/{world.TotalChunks} | Queue: {world.PendingWork}\n" +
            $"XYZ: {player.Position.X:F2} / {player.Position.Y:F2} / {player.Position.Z:F2}\n" +
            $"Holding: {hotbarNames[slot]}\n\n" +
            HighlightedBlockText(world, hasTarget, target, targetPrev);

        sprites.Begin(samplerState: SamplerState.PointClamp);
        // Retro drop shadow effect for clean readability
        sprites.DrawString(_font, debugText, new Vector2(11, 11), Color.Black);
        sprites.DrawString(_font, debugText, new Vector2(10, 10), Color.White);
        sprites.End();
    }

    private static string HighlightedBlockText(World world, bool hasTarget,
                                            (int x, int y, int z) target, (int x, int y, int z) front)
    {
        if (!hasTarget) return "Highlighted: none";

        byte id = world.GetBlock(target.x, target.y, target.z);
        string name = BlockRegistry.Instance.GetName(id);
        name = name.Substring(name.IndexOf(':') + 1).Replace('_', ' ');

        return
            $"Highlighted: {name} @ {target.x} {target.y} {target.z}\n" +
            $"  Light in front ({front.x} {front.y} {front.z}): {LightString(world, front)}\n" +
            $"  Light in block: {LightString(world, target)}";
    }
    
    private static string LightString(World world, (int x, int y, int z) p)
    {
        var light = world.Light;
        return $"Sky {light.Get(0, p.x, p.y, p.z)} -> {light.GetSkyNow(p.x, p.y, p.z)} | " +
            $"R {light.Get(1, p.x, p.y, p.z)} | G {light.Get(2, p.x, p.y, p.z)} | B {light.Get(3, p.x, p.y, p.z)} | " +
            $"Total {light.GetCombined(p.x, p.y, p.z)}";
    }
}