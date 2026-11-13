using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace CuboidFever;

// Polls the keyboard and mouse once per frame and answers questions in terms of actions.
// This is the only place that does edge detection, so no other class needs "wasDown" fields.
public sealed class ActionInput
{
    private readonly KeyBindings _bindings;
    private KeyboardState _kb, _prevKb;
    private MouseState _mouse, _prevMouse;

    public ActionInput(KeyBindings bindings) => _bindings = bindings;

    public KeyBindings Bindings => _bindings;

    // While true (the console is open and taking the keyboard) every action reads as not pressed and the wheel as
    // not moved, so typing "w" does not walk and a click does not break a block.
    public bool Blocked { get; set; }

    // How far the mouse wheel moved since the last frame (positive = scrolled up).
    public int ScrollDelta => Blocked ? 0 : _mouse.ScrollWheelValue - _prevMouse.ScrollWheelValue;

    // Cursor position in window coordinates. Only meaningful while the mouse is not captured
    // (InputManager.SetMouseCaptured(false)), i.e. in menus.
    public Point MousePosition => _mouse.Position;

    // True only on the frame the left button goes from up to down. Menus use this directly rather than
    // a GameAction, since clicking a button isn't something the player can rebind.
    public bool WasLeftClicked =>
        _mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton != ButtonState.Pressed;

    // True only on the frame the left button goes from down to up. Menu buttons fire on this rather than
    // on the press, so the click can't carry into gameplay (the break-block action is hold-to-repeat).
    public bool WasLeftReleased =>
        _mouse.LeftButton != ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Pressed;

    // Call exactly once per frame, at the top of Game1.Update.
    public void Update(KeyboardState kb, MouseState mouse)
    {
        _prevKb = _kb;
        _prevMouse = _mouse;
        _kb = kb;
        _mouse = mouse;
    }

    // True every frame the input is held.
    public bool IsDown(GameAction action) => !Blocked && IsDown(_bindings.Get(action), _kb, _mouse);

    // True only on the frame the input goes from up to down.
    public bool WasPressed(GameAction action)
    {
        if (Blocked) return false;

        var b = _bindings.Get(action);
        return IsDown(b, _kb, _mouse) && !IsDown(b, _prevKb, _prevMouse);
    }

    private static bool IsDown(Binding b, KeyboardState kb, MouseState mouse)
    {
        if (!b.IsMouse) return b.Key != Keys.None && kb.IsKeyDown(b.Key);

        return b.Mouse switch
        {
            GameMouseButton.Left   => mouse.LeftButton == ButtonState.Pressed,
            GameMouseButton.Right  => mouse.RightButton == ButtonState.Pressed,
            GameMouseButton.Middle => mouse.MiddleButton == ButtonState.Pressed,
            _ => false
        };
    }
}
