using Microsoft.Xna.Framework;

namespace CuboidFever;

public enum WindowMode
{
    Windowed,
    Borderless,
    Fullscreen
}

public class WindowManager
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly GameWindow _window;

    private Rectangle _windowedBounds;
    private WindowMode _currentMode = WindowMode.Windowed;

    public WindowMode CurrentMode => _currentMode;
    public int Width => _graphics.PreferredBackBufferWidth;
    public int Height => _graphics.PreferredBackBufferHeight;

    public WindowManager(GraphicsDeviceManager graphics, GameWindow window)
    {
        _graphics = graphics;
        _window = window;

        // Capture the initial windowed bounds at startup
        _windowedBounds = new Rectangle(
            _window.Position.X,
            _window.Position.Y,
            _graphics.PreferredBackBufferWidth,
            _graphics.PreferredBackBufferHeight
        );
    }

    public void CycleNextMode()
    {
        var next = _currentMode switch
        {
            WindowMode.Windowed => WindowMode.Borderless,
            WindowMode.Borderless => WindowMode.Fullscreen,
            _ => WindowMode.Windowed
        };
        SetWindowMode(next);
    }

    public void SetWindowMode(WindowMode mode)
    {
        // If we are leaving windowed mode, cache our current position/size first
        if (_currentMode == WindowMode.Windowed && mode != WindowMode.Windowed)
        {
            _windowedBounds = new Rectangle(
                _window.Position.X,
                _window.Position.Y,
                _graphics.PreferredBackBufferWidth,
                _graphics.PreferredBackBufferHeight
            );
        }

        switch (mode)
        {
            case WindowMode.Windowed:
                _graphics.IsFullScreen = false;
                _window.IsBorderless = false;
                _graphics.PreferredBackBufferWidth = _windowedBounds.Width;
                _graphics.PreferredBackBufferHeight = _windowedBounds.Height;
                _graphics.ApplyChanges();
                _window.Position = new Point(_windowedBounds.X, _windowedBounds.Y);
                break;

            case WindowMode.Borderless:
                _graphics.IsFullScreen = false;
                _window.IsBorderless = true;
                var currentDisplay = _graphics.GraphicsDevice.Adapter.CurrentDisplayMode;
                _graphics.PreferredBackBufferWidth = currentDisplay.Width;
                _graphics.PreferredBackBufferHeight = currentDisplay.Height;
                _graphics.ApplyChanges();
                
                // Force the window to the top-left origin of the screen to clear border offsets
                _window.Position = new Point(0, 0);
                break;

            case WindowMode.Fullscreen:
                _window.IsBorderless = false;
                _graphics.IsFullScreen = true;
                _graphics.ApplyChanges();
                break;
        }

        _currentMode = mode;
    }
}