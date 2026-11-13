using Microsoft.Xna.Framework;

namespace CuboidFever;

// A scorpion: wanders and idles until it spots the player, then chases them.
// Attacking (claws and sting) comes in a later step; for now it closes in and faces the player.
public sealed class ScorpionEntity : MobEntity
{
    private const float WalkAnimThreshold = 0.1f; // blocks per second above which the legs move

    public ScorpionEntity(EntityType type, Vector3 position, float yaw = 0f) : base(type, position, yaw)
    {
        MoveSpeed = 2.8f; // full speed, used when chasing; wandering is a gentle 45% of this

        // Highest priority first
        Goals.Add(new ChaseGoal(sightRange: 12f, loseRange: 18f, stopDistance: 0.9f, memorySeconds: 3f));
        Goals.Add(new WanderGoal(chance: 0.6f, minDistance: 2f, maxDistance: 6f, speed: 0.45f));
        Goals.Add(new IdleGoal(minSeconds: 1f, maxSeconds: 3f));
    }

    public override void Update(float dt, World world)
    {
        base.Update(dt, world); // thinks and moves
        Animate();
    }

    // Legs move only while it is actually travelling; standing still (or pushing against a wall) shows the resting pose
    private void Animate()
    {
        Play(HorizontalSpeed > WalkAnimThreshold ? "walk" : null);
    }
}
