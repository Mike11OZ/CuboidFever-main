using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

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

    public int Width => _graphics.GraphicsDevice.PresentationParameters.BackBufferWidth;
    public int Height => _graphics.GraphicsDevice.PresentationParameters.BackBufferHeight;

    public int ViewportWidth => _graphics.GraphicsDevice.Viewport.Width;
    public int ViewportHeight => _graphics.GraphicsDevice.Viewport.Height;
    public float ViewportAspectRatio => _graphics.GraphicsDevice.Viewport.AspectRatio;

    public event Action OnResolutionChanged;

    public WindowManager(GraphicsDeviceManager graphics, GameWindow window)
    {
        _graphics = graphics;
        _window = window;

        _windowedBounds = new Rectangle(
            _window.Position.X,
            _window.Position.Y,
            _window.ClientBounds.Width,
            _window.ClientBounds.Height
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
        // Preserve the current windowed dimensions before switching away.
        if (_currentMode == WindowMode.Windowed)
        {
            _windowedBounds = new Rectangle(
                _window.Position.X,
                _window.Position.Y,
                _window.ClientBounds.Width,
                _window.ClientBounds.Height
            );
        }

        var currentDisplay = _graphics.GraphicsDevice.Adapter.CurrentDisplayMode;

        switch (mode)
        {
            case WindowMode.Windowed:
                _graphics.IsFullScreen = false;
                _window.IsBorderless = false;
                _graphics.HardwareModeSwitch = true;

                _graphics.PreferredBackBufferWidth = _windowedBounds.Width;
                _graphics.PreferredBackBufferHeight = _windowedBounds.Height;

                _graphics.ApplyChanges();

                _window.Position = new Point(
                    _windowedBounds.X,
                    _windowedBounds.Y
                );
                break;

            case WindowMode.Borderless:
                _graphics.IsFullScreen = false;
                _window.IsBorderless = true;
                _graphics.HardwareModeSwitch = false;

                _graphics.PreferredBackBufferWidth = currentDisplay.Width;
                _graphics.PreferredBackBufferHeight = currentDisplay.Height;

                _graphics.ApplyChanges();

                _window.Position = Point.Zero;
                break;

            case WindowMode.Fullscreen:
                _window.IsBorderless = false;
                _graphics.IsFullScreen = true;
                _graphics.HardwareModeSwitch = true;

                _graphics.PreferredBackBufferWidth = currentDisplay.Width;
                _graphics.PreferredBackBufferHeight = currentDisplay.Height;

                _graphics.ApplyChanges();
                break;
        }

        _currentMode = mode;
        OnResolutionChanged?.Invoke();
    }
}