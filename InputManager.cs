using System.Runtime.InteropServices;

namespace CuboidFever;

public static class InputManager
{
    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern int SDL_SetRelativeMouseMode(bool enabled);

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool SDL_GetRelativeMouseMode();

    [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_GetRelativeMouseState(out int x, out int y);

    private static bool _isCaptured = true;

    public static void Initialize()
    {
        SetMouseCaptured(true);
    }

    public static void SetMouseCaptured(bool capture)
    {
        _isCaptured = capture;
        SDL_SetRelativeMouseMode(capture);
    }

    public static void GetMouseDelta(out int dx, out int dy)
    {
        if (_isCaptured)
        {
            SDL_GetRelativeMouseState(out dx, out dy);
        }
        else
        {
            dx = 0;
            dy = 0;
        }
    }
}