using Microsoft.Xna.Framework;

namespace CuboidFever;

// How an effect's colour is combined with the finished 3D scene.
public enum ScreenBlend
{
    // Normal overlay. The colour must be premultiplied, e.g. Color.Red * 0.4f (same as the HUD).
    Alpha,

    // scene * colour. White changes nothing, darker or coloured values tint the whole screen,
    // and a dark night scene stays dark. Best for tints (underwater, poison, ...).
    Multiply,

    // scene + colour * alpha. Best for flashes and glows.
    Additive,
}

// What effects are given each frame. Add fields here if a future effect needs more
// (player health, the day/night cycle, ...) - existing effects keep working unchanged.
public readonly record struct ScreenEffectContext(World World, Vector3 CameraPosition, float Time);

// Base class for full-screen effects drawn after the 3D world and before the HUD.
//
// To add a new effect:
//   1. Derive from ScreenEffect, keep whatever state it needs, update it in Update().
//   2. Return a colour + blend mode from TryGetOverlay() (return false while it has nothing to show).
//   3. Register it once: screenEffects.Add(new MyEffect());
//
// Example - a damage flash is a small class with a Trigger() method that sets a timer, an Update()
// that counts it down, and a TryGetOverlay() that returns (Color.Red * timerFraction, ScreenBlend.Alpha).
public abstract class ScreenEffect
{
    // Turn an effect off without removing it.
    public bool Enabled { get; set; } = true;

    // Lower values are drawn first (underneath). Effects with the same Order keep the order they were added in.
    public virtual int Order => 0;

    public virtual void Update(float dt, in ScreenEffectContext context) { }

    // Called once per frame after Update. Return false to skip drawing this frame.
    public abstract bool TryGetOverlay(out Color color, out ScreenBlend blend);
}
