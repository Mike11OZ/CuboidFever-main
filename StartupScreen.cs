using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// The game's startup phase: runs the resource-initialization steps one at a time while the loading
// screen shows a progress bar.
//
// A frame can't be drawn while a single long method is running, so initialization is split into steps.
// Update runs one step, then waits until Draw has put the new progress on screen before running the next.
// The bar therefore moves between steps rather than freezing for the whole load.
//
// Steps run in the order they were added, so a step may rely on all the steps before it.
public sealed class StartupScreen
{
    private readonly record struct Step(string Name, Action Run, float Weight);

    private readonly LoadingScreen _loadingScreen;
    private readonly List<Step> _steps = new();

    private float _totalWeight;
    private float _completedWeight;
    private int _nextStep;

    // False after each step until Draw has shown the updated progress. Without this, a slow step can
    // make MonoGame run several Updates in a row to catch up, and the following steps would run before
    // the bar had been redrawn.
    private bool _progressShown;

    public StartupScreen(LoadingScreen loadingScreen)
    {
        _loadingScreen = loadingScreen;
    }

    // True once every step has run and the finished (100%) bar has been drawn once.
    // The game moves on to whatever comes after startup.
    public bool IsComplete => _nextStep >= _steps.Count && _progressShown;

    // 0..1, weighted by each step's weight
    public float Progress => _totalWeight <= 0f ? 1f : _completedWeight / _totalWeight;

    // weight is how much of the bar this step takes up relative to the others. Slow steps should get a
    // bigger weight so the bar advances at a steady pace. The log printed after each step shows its time.
    public void AddStep(string name, Action run, float weight = 1f)
    {
        _steps.Add(new Step(name, run, weight));
        _totalWeight += weight;
    }

    // Call each frame until IsComplete is true.
    public void Update()
    {
        if (IsComplete || !_progressShown) return;

        var step = _steps[_nextStep];

        var timer = Stopwatch.StartNew();
        step.Run();
        Console.WriteLine($"[Startup] {step.Name} ({timer.ElapsedMilliseconds} ms)");

        _completedWeight += step.Weight;
        _nextStep++;
        _progressShown = false;
    }

    public void Draw(SpriteBatch sprites, GraphicsDevice graphicsDevice)
    {
        _loadingScreen.Draw(sprites, graphicsDevice, Progress);
        _progressShown = true;
    }
}
