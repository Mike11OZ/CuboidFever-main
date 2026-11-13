using System;
using System.Collections.Generic;

namespace CuboidFever;

// One thing a creature can be doing: wandering, standing about, chasing the player. A goal only decides what the
// creature wants (by setting its Navigator's target, or where it looks); MobEntity does the actual walking.
public abstract class Goal
{
    public virtual string Name => GetType().Name.Replace("Goal", "");

    // Should this goal begin now? Asked when the creature has nothing to do, in priority order.
    public abstract bool CanStart(MobEntity mob);

    // Should it keep going? Asked every frame while it runs. Returning false ends it.
    public virtual bool ShouldContinue(MobEntity mob) => true;

    // True for goals important enough to cut in on a lower-priority goal that is already running (chasing does).
    // Wandering and idling don't: they wait for the current one to finish.
    public virtual bool CanInterrupt => false;

    public virtual void Start(MobEntity mob) { }
    public abstract void Tick(MobEntity mob, float dt);
    public virtual void Stop(MobEntity mob) { }
}

// Runs one goal at a time. Goals are added highest priority first.
public sealed class GoalSelector
{
    private readonly List<Goal> _goals = new();
    private Goal _current;

    public void Add(Goal goal) => _goals.Add(goal);

    // What it is doing right now, for debugging (/entities)
    public string CurrentName => _current?.Name ?? "none";

    public void Tick(MobEntity mob, float dt)
    {
        // A more important goal that is allowed to interrupt takes over
        int currentIndex = _current == null ? _goals.Count : _goals.IndexOf(_current);
        for (int i = 0; i < currentIndex; i++)
        {
            if (!_goals[i].CanInterrupt || !_goals[i].CanStart(mob)) continue;

            Switch(mob, _goals[i]);
            break;
        }

        if (_current != null && !_current.ShouldContinue(mob))
            Switch(mob, null);

        // Nothing running: the highest-priority goal that wants to start gets it
        if (_current == null)
        {
            foreach (var goal in _goals)
            {
                if (!goal.CanStart(mob)) continue;

                Switch(mob, goal);
                break;
            }
        }

        _current?.Tick(mob, dt);
    }

    private void Switch(MobEntity mob, Goal next)
    {
        _current?.Stop(mob);
        _current = next;
        _current?.Start(mob);
    }
}
