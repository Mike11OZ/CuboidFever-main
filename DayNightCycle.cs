using System;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// Tracks the time of day and derives sky darkness, brightness and color from it.
// TimeOfDay runs 0..1: 0 = midnight, 0.25 = sunrise (6:00), 0.5 = noon, 0.75 = sunset (18:00).
public sealed class DayNightCycle
{
    // ---- Settings (loaded from data/cuboidfever/daynight.json) ----
    public float DayLengthSeconds { get; set; } = 600f;  // real seconds for a full 24 hour day
    public float StartHour { get; set; } = 8f;           // time of day the player spawns at (0-24)
    public int NightSkyLight { get; set; } = 3;          // level (0-15) that fully open sky drops to at night

    // ---- Runtime state ----
    public bool Paused { get; set; }
    public float TimeOfDay { get; private set; }
    public float Hour => TimeOfDay * 24f;

    private static readonly Color NightColor = new(8, 10, 28);
    private static readonly Color DayColor = new(100, 149, 237);   // CornflowerBlue
    private static readonly Color HorizonColor = new(255, 140, 80);

    private sealed class SettingsFile
    {
        public float? DayLengthSeconds { get; set; }
        public float? StartHour { get; set; }
        public int? NightSkyLight { get; set; }
    }

    // Missing or broken files fall back to the defaults above.
    public void LoadSettings(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"[DayNight] {path} not found, using defaults.");
                return;
            }

            var s = JsonSerializer.Deserialize<SettingsFile>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip });
            if (s == null) return;

            if (s.DayLengthSeconds is > 0f) DayLengthSeconds = s.DayLengthSeconds.Value;
            if (s.StartHour is >= 0f) StartHour = s.StartHour.Value % 24f;
            if (s.NightSkyLight is >= 0 and <= 15) NightSkyLight = s.NightSkyLight.Value;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DayNight] Could not read {path}, using defaults. ({ex.Message})");
        }
    }

    public void Reset() => TimeOfDay = Wrap(StartHour / 24f);

    public void Update(float dt)
    {
        if (Paused || DayLengthSeconds <= 0f) return;
        TimeOfDay = Wrap(TimeOfDay + dt / DayLengthSeconds);
    }

    // Jump forwards or backwards by a number of in-game hours.
    public void Scrub(float hours) => TimeOfDay = Wrap(TimeOfDay + hours / 24f);

    // -1 at midnight, 0 at sunrise and sunset, +1 at noon.
    public float SunHeight => -MathF.Cos(TimeOfDay * MathHelper.TwoPi);

    // 0 = full night, 1 = full day, easing through sunrise and sunset.
    private float DayFactor =>
        MathHelper.SmoothStep(0f, 1f, Math.Clamp((SunHeight + 0.2f) / 0.4f, 0f, 1f));

    // How many light levels the sky is darkened by right now: 0 at midday, 15 - NightSkyLight at night.
    public float SkyDarkness => (15 - NightSkyLight) * (1f - DayFactor);

    // Whole-number darkness for gameplay reads (LightMap.GetSkyNow / GetCombined).
    public int SkyDarknessLevels => (int)MathF.Round(SkyDarkness);

    // Shader multiplier for sky light. Because each light level is 0.8 of the one above
    // (Chunk.Curve), subtracting SkyDarkness levels equals multiplying by 0.8^SkyDarkness.
    public float SkyBrightness => MathF.Pow(0.8f, SkyDarkness);

    public Color SkyColor
    {
        get
        {
            Color c = Color.Lerp(NightColor, DayColor, DayFactor);

            // Warm glow while the sun is near the horizon
            float glow = Math.Clamp(1f - MathF.Abs(SunHeight) / 0.25f, 0f, 1f);
            return Color.Lerp(c, HorizonColor, glow * 0.55f);
        }
    }

    public string TimeString
    {
        get
        {
            int minutes = (int)(TimeOfDay * 24f * 60f) % (24 * 60);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }
    }

    private static float Wrap(float t) => t - MathF.Floor(t);
}