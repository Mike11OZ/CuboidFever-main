using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CuboidFever;

// Single source of truth for the water waves. The wave table lives here and is uploaded to
// WaterShader.fx every frame, so Height()/Slope() on the CPU and the shader always agree.
// Use Height() for buoyancy, boats, splash particles, etc. - no GPU readback needed.
public static class WaterWaves
{
    // ---- Runtime-tweakable settings ----
    public static float Intensity = 1.0f;   // scales every wave (was Chunk.WaterWaveIntensity)
    public static float Speed     = 0.8f;   // animation speed     (was Chunk.WaterWaveSpeed)

    // ---- Whitecap tuning ----
    public static float FoamSlope     = 0.30f; // surface slope where foam starts (rises with Intensity)
    public static float FoamSoftness  = 0.20f; // slope range over which it blends to full foam
    public static float FoamCrestMin  = 0.05f; // blocks above rest level before foam is allowed
    public static float FoamCrestSoft = 0.25f;
    public static float FoamBreakup   = 0.50f; // 0 = smooth blobs, 1 = very ragged
    public static float FoamNoiseScale = 1.4f; // noise cells per block
    public static float FoamWhiteness = 0.40f; // how much the water texture is washed out under foam
    public static float ShoreFoamWidth    = 0.40f; // blocks from the shore where foam ends completely
    public static float ShoreFoamSoftness = 0.28f; // blocks of fade at the outer edge (0 = hard edge)
    public static float ShoreFoamRagged   = 0.30f;

    // ---- Wave table ----
    public const int Count = 4;

    // wavelength (blocks), travel direction (degrees), amplitude (blocks at Intensity 1), phase offset
    private static readonly (float Wavelength, float Degrees, float Amp, float Phase)[] Defs =
    {
        (14.0f,  20f, 0.40f, 0.0f),
        ( 9.0f,  75f, 0.22f, 1.7f),
        ( 6.0f, 140f, 0.11f, 4.1f),
        ( 4.2f, 215f, 0.06f, 2.6f),
    };

    private const float OmegaScale = 1.1f; // omega = OmegaScale * sqrt(k): longer waves travel faster

    // Uploaded to the shader as WaveA / WaveB
    private static readonly Vector4[] WaveA = new Vector4[Count]; // kx, kz, omega, phase
    private static readonly Vector4[] WaveB = new Vector4[Count]; // amplitude, 0, 0, 0

    static WaterWaves()
    {
        for (int i = 0; i < Count; i++)
        {
            var d = Defs[i];
            float k = MathHelper.TwoPi / d.Wavelength;
            float rad = MathHelper.ToRadians(d.Degrees);

            WaveA[i] = new Vector4(
                k * MathF.Cos(rad),
                k * MathF.Sin(rad),
                OmegaScale * MathF.Sqrt(k),
                d.Phase);
            WaveB[i] = new Vector4(d.Amp, 0f, 0f, 0f);
        }
    }

    // Surface offset (blocks) from the water's rest level at world (x, z), game time in seconds.
    // Matches the shader exactly. Ignores the shoreline/covered-water rules, which are per vertex:
    // clamp with Math.Min(0, ...) under an occluding block and use 0 next to shore blocks.
    public static float Height(float x, float z, float time)
    {
        float t = time * Speed;
        float h = 0f;
        for (int i = 0; i < Count; i++)
        {
            var a = WaveA[i];
            h += WaveB[i].X * MathF.Sin(a.X * x + a.Y * z + a.Z * t + a.W);
        }
        return h * Intensity;
    }

    // Surface slope magnitude (rise per block) at world (x, z) - what the foam is driven by.
    public static float Slope(float x, float z, float time)
    {
        float t = time * Speed;
        float gx = 0f, gz = 0f;
        for (int i = 0; i < Count; i++)
        {
            var a = WaveA[i];
            float c = WaveB[i].X * MathF.Cos(a.X * x + a.Y * z + a.Z * t + a.W);
            gx += c * a.X;
            gz += c * a.Y;
        }
        return MathF.Sqrt(gx * gx + gz * gz) * Intensity;
    }

    public static void Apply(Effect fx, Matrix viewProjection, Vector3 cameraPos, Texture2D atlas, float time)
    {
        fx.Parameters["WorldViewProjection"]?.SetValue(viewProjection);
        fx.Parameters["CameraPosition"]?.SetValue(cameraPos);
        fx.Parameters["SpriteTexture"]?.SetValue(atlas);

        fx.Parameters["WaveTime"]?.SetValue(time * Speed);
        fx.Parameters["WaveIntensity"]?.SetValue(Intensity);

        // Set individual wave parameters
        fx.Parameters["W0A"]?.SetValue(WaveA[0]);
        fx.Parameters["W0B"]?.SetValue(WaveB[0]);
        fx.Parameters["W1A"]?.SetValue(WaveA[1]);
        fx.Parameters["W1B"]?.SetValue(WaveB[1]);
        fx.Parameters["W2A"]?.SetValue(WaveA[2]);
        fx.Parameters["W2B"]?.SetValue(WaveB[2]);
        fx.Parameters["W3A"]?.SetValue(WaveA[3]);
        fx.Parameters["W3B"]?.SetValue(WaveB[3]);

        fx.Parameters["FoamSlope"]?.SetValue(FoamSlope);
        fx.Parameters["FoamSoftness"]?.SetValue(FoamSoftness);
        fx.Parameters["FoamCrestMin"]?.SetValue(FoamCrestMin);
        fx.Parameters["FoamCrestSoft"]?.SetValue(FoamCrestSoft);
        fx.Parameters["FoamBreakup"]?.SetValue(FoamBreakup);
        fx.Parameters["FoamNoiseScale"]?.SetValue(FoamNoiseScale);
        fx.Parameters["FoamWhiteness"]?.SetValue(FoamWhiteness);
        fx.Parameters["ShoreFoamWidth"]?.SetValue(ShoreFoamWidth);
        fx.Parameters["ShoreFoamSoftness"]?.SetValue(ShoreFoamSoftness);
        fx.Parameters["ShoreFoamRagged"]?.SetValue(ShoreFoamRagged);
    }
}
