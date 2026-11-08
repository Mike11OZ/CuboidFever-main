using System;

namespace CuboidFever;

// The editing rules of a one-line text box, with no drawing and no graphics types so they can be tested.
// Characters are added at the end and Backspace removes the last one; there is no cursor to move around.
public sealed class TextFieldModel
{
    private string _text = string.Empty;

    public int MaxLength { get; }

    // Which characters may be typed. Anything else is ignored.
    public Func<char, bool> Accepts { get; }

    public TextFieldModel(int maxLength, Func<char, bool> accepts = null)
    {
        MaxLength = Math.Max(0, maxLength);
        Accepts = accepts ?? (_ => true);
    }

    public string Text
    {
        get => _text;
        set
        {
            // Same rules as typing, so text set from code can never break them
            _text = string.Empty;
            foreach (char c in value ?? string.Empty)
                Type(c);
        }
    }

    public bool IsEmpty => _text.Length == 0;

    // Adds a character if it is allowed and there is room. Returns whether it was added.
    public bool Type(char c)
    {
        if (_text.Length >= MaxLength || !Accepts(c)) return false;

        _text += c;
        return true;
    }

    public bool Backspace()
    {
        if (_text.Length == 0) return false;

        _text = _text.Substring(0, _text.Length - 1);
        return true;
    }

    // What the window's text-input event gives for a key press: a backspace arrives as '\b', other control
    // characters (Enter, Tab, Escape) are not text and are ignored here. Returns whether the text changed.
    public bool Handle(char c) => c == '\b' ? Backspace() : Type(c);

    public void Clear() => _text = string.Empty;
}
