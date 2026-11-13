using System;
using Microsoft.Xna.Framework;

namespace CuboidFever;

// Stands still for a while. Always willing to start, so it goes last and is what a creature does when nothing else wants to.
public sealed class IdleGoal : Goal
{
    private readonly float _minSeconds, _maxSeconds;
    private float _timer;

    public IdleGoal(float minSeconds = 1f, float maxSeconds = 3f)
    {
        _minSeconds = minSeconds;
        _maxSeconds = maxSeconds;
    }

    public override bool CanStart(MobEntity mob) => true;
    public override bool ShouldContinue(MobEntity mob) => _timer > 0f;

    public override void Start(MobEntity mob)
    {
        _timer = _minSeconds + Random.Shared.NextSingle() * (_maxSeconds - _minSeconds);
        mob.Nav.Stop();
    }

    public override void Tick(MobEntity mob, float dt) => _timer -= dt;
}

// Strolls to a random spot a few blocks away, slowly. Gives up if it gets stuck or the trip takes too long.
public sealed class WanderGoal : Goal
{
    private readonly float _chance;     // chance of wanting to wander each time the creature picks something to do
    private readonly float _minDistance, _maxDistance;
    private readonly float _speed;      // 0..1 of the creature's full speed
    private const float GiveUpSeconds = 8f;

    private float _elapsed;

    public WanderGoal(float chance = 0.6f, float minDistance = 2f, float maxDistance = 6f, float speed = 0.45f)
    {
        _chance = chance;
        _minDistance = minDistance;
        _maxDistance = maxDistance;
        _speed = speed;
    }

    public override bool CanStart(MobEntity mob) => Random.Shared.NextSingle() < _chance;

    public override bool ShouldContinue(MobEntity mob) =>
        mob.Nav.HasTarget && !mob.Nav.Arrived && !mob.Nav.IsStuck && _elapsed < GiveUpSeconds;

    public override void Start(MobEntity mob)
    {
        _elapsed = 0f;

        float heading = Random.Shared.NextSingle() * MathHelper.TwoPi;
        float distance = _minDistance + Random.Shared.NextSingle() * (_maxDistance - _minDistance);
        var target = mob.Position + new Vector3(MathF.Cos(heading), 0f, MathF.Sin(heading)) * distance;

        mob.Nav.MoveTo(target, _speed);
    }

    public override void Tick(MobEntity mob, float dt) => _elapsed += dt;

    public override void Stop(MobEntity mob) => mob.Nav.Stop();
}

// Goes after the player once it can see them, and keeps going for a few seconds after losing sight.
// Stops a short way off and faces the player, which is where the attack goal (a later step) takes over.
public sealed class ChaseGoal : Goal
{
    private readonly float _sightRange;      // how far away it notices the player
    private readonly float _loseRange;       // how far away it gives up, even while it can still see them
    private readonly float _stopDistance;    // how close it gets before standing still
    private readonly float _memorySeconds;   // how long it keeps heading for the last place it saw the player

    private float _sinceSeen;
    private Vector3 _lastKnown;
    private bool _lost;

    public ChaseGoal(float sightRange = 12f, float loseRange = 18f, float stopDistance = 0.9f, float memorySeconds = 3f)
    {
        _sightRange = sightRange;
        _loseRange = loseRange;
        _stopDistance = stopDistance;
        _memorySeconds = memorySeconds;
    }

    public override bool CanInterrupt => true;

    public override bool CanStart(MobEntity mob)
    {
        var player = mob.Manager?.Player;
        return player != null && Distance(mob, player) <= _sightRange && CanSee(mob, player);
    }

    public override bool ShouldContinue(MobEntity mob)
    {
        var player = mob.Manager?.Player;
        if (player == null || _lost) return false;

        return Distance(mob, player) <= _loseRange && _sinceSeen < _memorySeconds;
    }

    public override void Start(MobEntity mob)
    {
        _sinceSeen = 0f;
        _lost = false;
        _lastKnown = mob.Manager.Player.Position;
    }

    public override void Tick(MobEntity mob, float dt)
    {
        var player = mob.Manager.Player;
        bool sees = CanSee(mob, player);

        if (sees)
        {
            _sinceSeen = 0f;
            _lastKnown = player.Position;
        }
        else
        {
            _sinceSeen += dt;
        }

        float flatDistance = FlatDistance(mob.Position, _lastKnown);

        if (sees && flatDistance <= _stopDistance)
        {
            // Close enough: stand and face them
            mob.Nav.Stop();
            mob.LookTarget = player.Position;
            return;
        }

        if (!sees && flatDistance <= _stopDistance)
        {
            // Reached where the player was last seen and they are not there: lose interest
            _lost = true;
            return;
        }

        mob.Nav.MoveTo(_lastKnown, 1f, _stopDistance * 0.8f);
    }

    public override void Stop(MobEntity mob) => mob.Nav.Stop();

    // =====================  SENSES  =====================

    private static float Distance(MobEntity mob, Player player) => Vector3.Distance(mob.Position, player.Position);

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.X - b.X, dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dz * dz);
    }

    // Is there a clear line from the creature's eyes to the player's head or body? Solid blocks in between stop it
    // (plants and water don't).
    private static bool CanSee(MobEntity mob, Player player)
    {
        var world = mob.Manager?.World;
        if (world == null) return false;

        Vector3 eyes = mob.Position + new Vector3(0f, mob.Height * 0.6f, 0f);
        return ClearLine(world, eyes, player.Eye) || ClearLine(world, eyes, player.Position + new Vector3(0f, 0.9f, 0f));
    }

    private static bool ClearLine(World world, Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float length = delta.Length();
        if (length < 0.001f) return true;

        const float Step = 0.25f;
        int steps = (int)MathF.Ceiling(length / Step);

        // The two end points are inside the creature's and the player's own blocks, so only the points between count
        for (int i = 1; i < steps; i++)
        {
            Vector3 p = from + delta * (i / (float)steps);
            if (world.IsSolid((int)MathF.Floor(p.X), (int)MathF.Floor(p.Y), (int)MathF.Floor(p.Z))) return false;
        }

        return true;
    }
}
