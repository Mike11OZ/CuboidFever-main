using System;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// Tracks the time of day and derives the sky brightness and color from it.
// TimeOfDay runs 0..1: 0 = midnight, 0.25 = sunrise (6:00), 0.5 = noon, 0.75 = sunset (18:00).
public sealed class DayNightCycle
{
    // ---- Settings (loaded from data/cuboidfever/daynight.json) ----
    public float DayLengthSeconds { get; set; } = 600f;   // real seconds for a full 24 hour day
    public float StartHour { get; set; } = 8f;            // time of day the player spawns at (0-24)
    public float MinSkyBrightness { get; set; } = 0.12f;  // how bright full sky light is at night (0-1)

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
        public float? MinSkyBrightness { get; set; }
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
            if (s.MinSkyBrightness is >= 0f and <= 1f) MinSkyBrightness = s.MinSkyBrightness.Value;
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

    // Multiplier applied to sky light. 1 in full daylight, MinSkyBrightness at night,
    // easing between the two around sunrise and sunset.
    public float SkyBrightness
    {
        get
        {
            float t = MathHelper.SmoothStep(0f, 1f, Math.Clamp((SunHeight + 0.2f) / 0.4f, 0f, 1f));
            return MathHelper.Lerp(MinSkyBrightness, 1f, t);
        }
    }

    public Color SkyColor
    {
        get
        {
            float h = SunHeight;
            float day = MathHelper.SmoothStep(0f, 1f, Math.Clamp((h + 0.2f) / 0.4f, 0f, 1f));
            Color c = Color.Lerp(NightColor, DayColor, day);

            // Warm glow while the sun is near the horizon
            float glow = Math.Clamp(1f - MathF.Abs(h) / 0.25f, 0f, 1f);
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