using System;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// Gets a creature to a point. Goals say where; this sets the creature's Wish each frame to walk there.
//
// Right now it walks in a straight line, so it can only get around things by hopping one-block ledges and
// otherwise gets stuck against walls. Keeping this in its own class is deliberate: pathfinding (the next step)
// replaces the straight line in here and no goal needs to change.
public sealed class Navigator
{
    private const float StuckCheckSeconds = 1f;
    private const float StuckDistance = 0.3f; // moved less than this in a check = stuck

    private readonly MobEntity _mob;
    private Vector3 _target;
    private float _speed;          // fraction of the mob's MoveSpeed, 0..1
    private float _arriveDistance;

    private Vector3 _sample;
    private float _sampleTimer;

    public bool HasTarget { get; private set; }
    public bool Arrived { get; private set; }

    // True when it has been trying to move but got nowhere over the last check. Goals use it to give up.
    public bool IsStuck { get; private set; }

    public Navigator(MobEntity mob) => _mob = mob;

    // Head for 'target' (only X and Z matter), at 'speed' (0..1 of full speed), until within 'arriveDistance'.
    // Safe to call every frame with a moving target: it only restarts the stuck check when it had no target before.
    public void MoveTo(Vector3 target, float speed, float arriveDistance = 0.3f)
    {
        if (!HasTarget)
        {
            Arrived = false;
            IsStuck = false;
            _sample = _mob.Position;
            _sampleTimer = 0f;
        }

        HasTarget = true;
        _target = target;
        _speed = MathHelper.Clamp(speed, 0f, 1f);
        _arriveDistance = arriveDistance;
    }

    public void Stop()
    {
        HasTarget = false;
        Arrived = false;
        IsStuck = false;
        _mob.Wish = Vector3.Zero;
    }

    // Once per frame, before the mob moves
    public void Tick(float dt)
    {
        if (!HasTarget) return;

        float dx = _target.X - _mob.Position.X;
        float dz = _target.Z - _mob.Position.Z;
        float distance = MathF.Sqrt(dx * dx + dz * dz);

        if (distance <= _arriveDistance)
        {
            Arrived = true;
            _mob.Wish = Vector3.Zero;
            return;
        }

        Arrived = false;
        _mob.Wish = new Vector3(dx / distance, 0f, dz / distance) * _speed;

        _sampleTimer += dt;
        if (_sampleTimer >= StuckCheckSeconds)
        {
            float mx = _mob.Position.X - _sample.X, mz = _mob.Position.Z - _sample.Z;
            IsStuck = MathF.Sqrt(mx * mx + mz * mz) < StuckDistance;

            _sample = _mob.Position;
            _sampleTimer = 0f;
        }
    }
}
