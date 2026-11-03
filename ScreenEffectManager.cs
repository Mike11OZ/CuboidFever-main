using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// Owns the list of ScreenEffects, updates them, and draws each one as a full-screen quad.
// It knows nothing about any particular effect, so new effects never require changes here.
public sealed class ScreenEffectManager : IDisposable
{
    // result = scene * source colour
    private static readonly BlendState MultiplyBlend = new()
    {
        ColorSourceBlend = Blend.Zero,
        ColorDestinationBlend = Blend.SourceColor,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.SourceAlpha,
    };

    private readonly GraphicsDevice _gd;
    private readonly SpriteBatch _sprites;
    private readonly Texture2D _pixel;
    private readonly List<ScreenEffect> _effects = new();

    public ScreenEffectManager(GraphicsDevice graphicsDevice, SpriteBatch sprites)
    {
        _gd = graphicsDevice;
        _sprites = sprites;

        _pixel = new Texture2D(_gd, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public T Add<T>(T effect) where T : ScreenEffect
    {
        // Insert after every effect with the same or lower Order so equal Orders stay in add order.
        int index = _effects.Count;
        while (index > 0 && _effects[index - 1].Order > effect.Order) index--;
        _effects.Insert(index, effect);
        return effect;
    }

    public bool Remove(ScreenEffect effect) => _effects.Remove(effect);

    // First effect of the given type, or null.
    public T Get<T>() where T : ScreenEffect
    {
        foreach (var e in _effects)
            if (e is T match) return match;
        return null;
    }

    public void Update(float dt, in ScreenEffectContext context)
    {
        foreach (var e in _effects)
            if (e.Enabled) e.Update(dt, in context);
    }

    public void Draw()
    {
        Rectangle screen = _gd.Viewport.Bounds;

        foreach (var e in _effects)
        {
            if (!e.Enabled || !e.TryGetOverlay(out Color color, out ScreenBlend blend)) continue;

            _sprites.Begin(SpriteSortMode.Deferred, BlendFor(blend), SamplerState.PointClamp);
            _sprites.Draw(_pixel, screen, color);
            _sprites.End();
        }
    }

    private static BlendState BlendFor(ScreenBlend blend) => blend switch
    {
        ScreenBlend.Multiply => MultiplyBlend,
        ScreenBlend.Additive => BlendState.Additive,
        _ => BlendState.AlphaBlend,
    };

    public void Dispose() => _pixel.Dispose();
}
