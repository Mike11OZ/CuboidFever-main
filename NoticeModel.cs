using System;

namespace CuboidFever;

// A short message shown on the menu screens ("Could not open 'World 3': ...") that fades away by itself.
// Only the timing lives here, so it can be tested; MainMenu draws it.
public sealed class NoticeModel
{
    public const float DefaultSeconds = 6f;
    public const float FadeSeconds = 1f;

    private float _remaining;
    private float _duration;

    public string Text { get; private set; } = string.Empty;

    public bool IsActive => _remaining > 0f && Text.Length > 0;

    // Shows a message, replacing any message already showing, and restarts the timer.
    public void Show(string text, float seconds = DefaultSeconds)
    {
        Text = text ?? string.Empty;
        _duration = Math.Max(0f, seconds);
        _remaining = _duration;
    }

    public void Update(float deltaTime)
    {
        if (_remaining > 0f)
            _remaining = Math.Max(0f, _remaining - deltaTime);
    }

    // Hides it right away (the player clicked on to something else).
    public void Dismiss() => _remaining = 0f;

    // 1 while fully visible, falling to 0 over the last FadeSeconds. A message shorter than the fade just fades.
    public float Opacity
    {
        get
        {
            if (!IsActive) return 0f;

            float fade = Math.Min(FadeSeconds, _duration);
            return fade <= 0f ? 1f : Math.Clamp(_remaining / fade, 0f, 1f);
        }
    }
}
