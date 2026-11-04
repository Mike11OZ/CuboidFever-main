using Microsoft.Xna.Framework.Input;

namespace CuboidFever;

public enum GameMouseButton { Left, Middle, Right }

// One physical input: either a keyboard key or a mouse button.
public readonly record struct Binding(Keys Key, GameMouseButton? Mouse = null)
{
    public bool IsMouse => Mouse.HasValue;

    public static implicit operator Binding(Keys key) => new(key);
    public static Binding FromMouse(GameMouseButton button) => new(Keys.None, button);

    public override string ToString() => IsMouse ? $"Mouse {Mouse}" : Key.ToString();
}