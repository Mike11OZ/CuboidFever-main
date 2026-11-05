using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

public class DebugOverlay
{
    private readonly SpriteFont _font;
    private readonly SpriteBatch _sprites;
    private readonly ActionInput _input;
    private readonly World _world;
    private readonly Player _player;
    private readonly FrameRateLimiter _fps;
    private readonly Hotbar _hotbar;
    private readonly WindowManager _windowManager;
    private readonly DayNightCycle _dayNight;
    private readonly AudioManager _audio;
    private readonly BlockInteraction _interaction;

    private bool _isVisible;
    private int _frames;
    private double _lastFpsTime;
    private int _currentFps;

    // CPU and RAM tracking fields
    private readonly Process _currentProcess;
    private TimeSpan _lastCpuTime;
    private double _cpuUsagePercent;
    private long _memoryMb;

    public bool IsVisible => _isVisible;

    public DebugOverlay(SpriteFont font, SpriteBatch sprites, ActionInput input, World world, Player player,
                        FrameRateLimiter fps, Hotbar hotbar, WindowManager windowManager,
                        DayNightCycle dayNight, AudioManager audio, BlockInteraction interaction)
    {
        _font = font;
        _sprites = sprites;
        _input = input;
        _world = world;
        _player = player;
        _fps = fps;
        _hotbar = hotbar;
        _windowManager = windowManager;
        _dayNight = dayNight;
        _audio = audio;
        _interaction = interaction;

        _currentProcess = Process.GetCurrentProcess();
        _lastCpuTime = _currentProcess.TotalProcessorTime;
    }

    public void Update()
    {
        if (_input.WasPressed(GameAction.ToggleDebugOverlay))
            _isVisible = !_isVisible;
    }

    public void UpdateFps(GameTime gameTime)
    {
        _frames++;

        double currentTime = gameTime.TotalGameTime.TotalSeconds;
        double elapsed = currentTime - _lastFpsTime;

        if (elapsed >= 1.0)
        {
            _currentFps = (int)Math.Round(_frames / elapsed);
            _frames = 0;
            _lastFpsTime = currentTime;

            // Update CPU and RAM metrics once per second alongside the FPS calculation
            _currentProcess.Refresh();
            _memoryMb = _currentProcess.WorkingSet64 / (1024 * 1024);

            TimeSpan currentCpuTime = _currentProcess.TotalProcessorTime;
            double cpuTimeDelta = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
            double realTimeDelta = elapsed * 1000.0;

            if (realTimeDelta > 0)
            {
                _cpuUsagePercent = (cpuTimeDelta / (realTimeDelta * Environment.ProcessorCount)) * 100.0;
            }

            _lastCpuTime = currentCpuTime;
        }
    }

    public void Draw()
    {
        if (!_isVisible || _font == null) return;

        int cap = _fps.CurrentCap;
        string fpsStr = cap == 0 ? "none" : cap.ToString();

        string debugText =
            $"{GameInfo.FullTitle}\n" +
            $"Display: {_windowManager.CurrentMode} ({_windowManager.Width}x{_windowManager.Height}) | " +
            $"Viewport: {_windowManager.ViewportWidth}x{_windowManager.ViewportHeight} ({_windowManager.ViewportAspectRatio:F4})\n" +
            $"FPS: {_currentFps} (cap: {fpsStr}) | CPU: {_cpuUsagePercent:F1}% | RAM: {_memoryMb} MB\n" +
            $"Leaves: {ChunkMesher.LeafCullDepth} | AO: {(ChunkMesher.AmbientOcclusion ? "on" : "off")}\n" +
            $"Time: {_dayNight.TimeString} | Day: {_dayNight.DayLengthSeconds:F0}s | Sky darkness: -{_dayNight.SkyDarknessLevels}{(_dayNight.Paused ? " | PAUSED" : "")}\n" +
            $"Distance: {_world.LoadRadius} | Chunks: {_world.VisibleChunks}/{_world.TotalChunks} | Queue: {_world.PendingWork} | Block updates: {_world.PendingBlockUpdates}\n" +
            $"XYZ: {_player.Position.X:F2} / {_player.Position.Y:F2} / {_player.Position.Z:F2}\n" +
            BiomeText() +
            $"Holding: {_hotbar.SelectedName} | Music: {_audio.CurrentSongName}\n\n" +
            HighlightedBlockText();

        _sprites.Begin(samplerState: SamplerState.PointClamp);
        _sprites.DrawString(_font, debugText, new Vector2(11, 11), Color.Black);
        _sprites.DrawString(_font, debugText, new Vector2(10, 10), Color.White);
        _sprites.End();
    }

    private string BiomeText()
    {
        int x = (int)System.Math.Floor(_player.Position.X);
        int z = (int)System.Math.Floor(_player.Position.Z);
        var (temp, humid) = _world.GetClimateAt(x, z);

        return $"Biome: {_world.GetBiomeAt(x, z)} | Temp: {temp:F2} | Humidity: {humid:F2} | Hilliness: {ClimateMap.Hilliness(temp, humid):F2}\n";
    }

    private string HighlightedBlockText()
    {
        if (!_interaction.HasTarget) return "Highlighted: none";

        var target = _interaction.Target;
        var front = _interaction.TargetPrev;

        byte id = _world.GetBlock(target.x, target.y, target.z);
        string name = BlockRegistry.Instance.GetName(id);
        name = name.Substring(name.IndexOf(':') + 1).Replace('_', ' ');

        return
            $"Highlighted: {name} @ {target.x} {target.y} {target.z}\n" +
            $"  Light in front ({front.x} {front.y} {front.z}): {LightString(front)}\n" +
            $"  Light in block: {LightString(target)}";
    }

    private string LightString((int x, int y, int z) p)
    {
        var light = _world.Light;
        return $"Sky {light.Get(0, p.x, p.y, p.z)} -> {light.GetSkyNow(p.x, p.y, p.z)} | " +
            $"R {light.Get(1, p.x, p.y, p.z)} | G {light.Get(2, p.x, p.y, p.z)} | B {light.Get(3, p.x, p.y, p.z)} | " +
            $"Total {light.GetCombined(p.x, p.y, p.z)}";
    }
}