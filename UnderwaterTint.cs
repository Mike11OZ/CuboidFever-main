using System;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// Blue tint while the camera is inside water. "Inside" is decided with WaterSurface, which rebuilds the
// wave-displaced surface exactly as the water shader draws it, so the tint switches on the moment the
// camera dips under a wave crest and off again in a trough - not at the flat block boundary.
public sealed class UnderwaterTint : ScreenEffect
{
    // Colour the scene is multiplied by when fully submerged (white = no tint).
    public Color Tint { get; set; } = new Color(120, 170, 255);

    // Seconds to fade in/out, so bobbing at the surface doesn't flicker the screen.
    public float FadeSeconds { get; set; } = 0.15f;

    // True while the camera is below the drawn water surface (not faded). Handy for other systems
    // later, e.g. muffling audio or slowing movement.
    public bool IsSubmerged { get; private set; }

    private float _amount; // 0 = out of water, 1 = fully tinted

    public override void Update(float dt, in ScreenEffectContext context)
    {
        IsSubmerged = WaterSurface.IsUnderwater(context.World, context.CameraPosition, context.Time);

        float target = IsSubmerged ? 1f : 0f;
        float maxStep = FadeSeconds > 0f ? dt / FadeSeconds : 1f;
        _amount += Math.Clamp(target - _amount, -maxStep, maxStep);
    }

    public override bool TryGetOverlay(out Color color, out ScreenBlend blend)
    {
        blend = ScreenBlend.Multiply;
        color = Color.Lerp(Color.White, Tint, _amount);
        return _amount > 0.001f;
    }
}
