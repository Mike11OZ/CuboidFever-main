using System;
using Microsoft.Xna.Framework;

namespace CuboidFever;

public sealed class DayNightCycle
{
    private const float DefaultDayLengthSeconds = 1200f; // 20 minutes per full day/night cycle

    public float DayLengthSeconds { get; set; } = DefaultDayLengthSeconds;
    public float TimeOfDay { get; set; } // 0.0 to 1.0 (0 = sunrise, 0.25 = noon, 0.5 = sunset, 0.75 = midnight)
    public bool Paused { get; set; }

    public int SkyDarkness { get; private set; }
    public int SkyDarknessLevels => 15;

    public Color SkyColor { get; private set; } = Color.CornflowerBlue;
    public float SkyBrightness { get; private set; } = 1f;

    // -1 at midnight, 0 at sunrise and sunset, +1 at noon.
    public float SunHeight => -MathF.Cos(TimeOfDay * MathHelper.TwoPi);

    // Unit vector toward the sun: rises in +Z, passes overhead at noon, sets in -Z.
    public Vector3 SunDirection => new(0f, SunHeight, MathF.Sin(TimeOfDay * MathHelper.TwoPi));

    // 0 = full night, 1 = full day, easing through sunrise and sunset.
    private float DayFactor =>
        MathHelper.SmoothStep(0f, 1f, Math.Clamp((SunHeight + 0.2f) / 0.4f, 0f, 1f));

    public string TimeString
    {
        get
        {
            float hours = (TimeOfDay * 24f + 6f) % 24f; // 0.0 = 06:00 (sunrise)
            int h = (int)hours;
            int m = (int)((hours - h) * 60f);
            return $"{h:D2}:{m:D2}";
        }
    }

    public void Update(float deltaTime)
    {
        if (!Paused && DayLengthSeconds > 0f)
        {
            TimeOfDay += deltaTime / DayLengthSeconds;
            TimeOfDay %= 1.0f;
        }

        UpdateSky();
    }

    public void Scrub(float hours)
    {
        TimeOfDay += hours / 24f;
        TimeOfDay = (TimeOfDay % 1f + 1f) % 1f;
        UpdateSky();
    }

    public void Reset()
    {
        TimeOfDay = 0f;
        Paused = false;
        DayLengthSeconds = DefaultDayLengthSeconds;
        UpdateSky();
    }

    public void LoadSettings(object settings)
    {
        // Accepts the settings argument passed from Game1
        UpdateSky();
    }

    private void UpdateSky()
    {
        float dayFactor = DayFactor;

        Color nightColor = new Color(10, 15, 30);
        Color sunsetColor = new Color(230, 100, 50);
        Color dayColor = new Color(135, 206, 235);

        if (dayFactor < 0.5f)
        {
            float t = dayFactor * 2f;
            SkyColor = Color.Lerp(nightColor, sunsetColor, t);
        }
        else
        {
            float t = (dayFactor - 0.5f) * 2f;
            SkyColor = Color.Lerp(sunsetColor, dayColor, t);
        }

        SkyBrightness = MathHelper.Lerp(0.15f, 1f, dayFactor);

        float lightLevel = MathHelper.Lerp(2f, 15f, 1f - dayFactor);
        SkyDarkness = (int)Math.Clamp(lightLevel, 0, 15);
    }
}