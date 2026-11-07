using System;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// Turns input into player movement and keeps track of where the camera is looking.
// Player does the physics and collision; this class decides what the player wants to do.
public sealed class PlayerController
{
    private const float MouseSensitivity = 0.003f;
    private const float MaxPitch = 1.5f;

    // Where the camera sits while the first columns load, before the player has spawned.
    private static readonly Vector3 LoadingCameraPosition = new(128.5f, 100f, 128.5f);
    private static readonly Vector3 RespawnPoint = new(128.5f, 120f, 128.5f);
    private const float VoidY = -20f;

    private readonly Player _player;
    private readonly World _world;
    private readonly ActionInput _input;

    private float _yaw, _pitch;

    public PlayerController(Player player, World world, ActionInput input)
    {
        _player = player;
        _world = world;
        _input = input;
        CamPos = LoadingCameraPosition;
    }

    // ---- Camera state, read by rendering and block interaction ----
    public Vector3 CamPos { get; private set; }

    public Matrix Rotation => Matrix.CreateFromYawPitchRoll(_yaw, _pitch, 0);
    public Vector3 Forward => Vector3.Transform(Vector3.Forward, Rotation);
    public Vector3 Right => Vector3.Transform(Vector3.Right, Rotation);
    public Vector3 Up => Vector3.Transform(Vector3.Up, Rotation);
    public Matrix View => Matrix.CreateLookAt(CamPos, CamPos + Forward, Vector3.Up);

    // Where the player is looking, in radians. Saved with the world.
    public float Yaw => _yaw;
    public float Pitch => _pitch;

    // Puts the player at a position (feet) and snaps the camera to their eyes.
    public void Spawn(Vector3 feetPosition)
    {
        _player.Position = feetPosition;
        CamPos = _player.Eye;
    }

    // Where the camera sits while the world around the player loads. The world loads around the camera, so for a
    // saved player who is far from the default spot it has to start near where they will appear.
    public void SetLoadingCamera(Vector3 position) => CamPos = position;

    // Restores a saved look direction. Values that are not real numbers are ignored.
    public void SetLook(float yaw, float pitch)
    {
        if (!float.IsFinite(yaw) || !float.IsFinite(pitch)) return;

        _yaw = yaw;
        _pitch = MathHelper.Clamp(pitch, -MaxPitch, MaxPitch);
    }

    // Call once per frame while playing. 'windowActive' stops the mouse look when the window is unfocused.
    public void Update(float dt, bool windowActive)
    {
        if (windowActive) ApplyMouseLook();

        if (_input.WasPressed(GameAction.ToggleFly)) _player.Flying = !_player.Flying;

        Vector3 forward = Forward, right = Right;
        if (!_player.Flying)
        {
            // Walking moves along the ground plane no matter where you look
            forward = new Vector3(forward.X, 0, forward.Z);
            if (forward != Vector3.Zero) forward.Normalize();
            right = new Vector3(right.X, 0, right.Z);
            if (right != Vector3.Zero) right.Normalize();
        }

        Vector3 wish = Vector3.Zero;
        if (_input.IsDown(GameAction.MoveForward)) wish += forward;
        if (_input.IsDown(GameAction.MoveBack)) wish -= forward;
        if (_input.IsDown(GameAction.MoveRight)) wish += right;
        if (_input.IsDown(GameAction.MoveLeft)) wish -= right;

        bool jump = _input.IsDown(GameAction.Jump);
        float vertical = (jump ? 1f : 0f) - (_input.IsDown(GameAction.Descend) ? 1f : 0f);

        // Don't simulate walking physics until the ground under the player exists
        bool groundLoaded = _world.IsColumnLoaded((int)MathF.Floor(_player.Position.X),
                                                  (int)MathF.Floor(_player.Position.Z));
        if (_player.Flying || groundLoaded)
            _player.Update(_world, dt, wish, jump, vertical);

        if (_player.Position.Y < VoidY) _player.Position = RespawnPoint;

        CamPos = _player.Eye;
    }

    private void ApplyMouseLook()
    {
        InputManager.GetMouseDelta(out int dx, out int dy);
        if (dx == 0 && dy == 0) return;

        _yaw -= dx * MouseSensitivity;
        _pitch -= dy * MouseSensitivity;
        _pitch = MathHelper.Clamp(_pitch, -MaxPitch, MaxPitch);
    }
}
