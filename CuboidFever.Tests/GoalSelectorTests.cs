using System.Collections.Generic;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

public class GoalSelectorTests
{
    // A goal the test controls. The selector never looks inside the creature, so tests pass null for it.
    private sealed class FakeGoal : Goal
    {
        public bool Wants, Keeps = true, Interrupts;
        public readonly List<string> Log;
        private readonly string _name;

        public FakeGoal(string name, List<string> log) { _name = name; Log = log; }

        public override string Name => _name;
        public override bool CanInterrupt => Interrupts;
        public override bool CanStart(MobEntity mob) => Wants;
        public override bool ShouldContinue(MobEntity mob) => Keeps;
        public override void Start(MobEntity mob) => Log.Add("start " + _name);
        public override void Stop(MobEntity mob) => Log.Add("stop " + _name);
        public override void Tick(MobEntity mob, float dt) => Log.Add("tick " + _name);
    }

    [Fact]
    public void StartsTheHighestPriorityGoalThatWantsToStart()
    {
        var log = new List<string>();
        var chase = new FakeGoal("chase", log) { Wants = false };
        var wander = new FakeGoal("wander", log) { Wants = true };
        var idle = new FakeGoal("idle", log) { Wants = true };

        var selector = new GoalSelector();
        selector.Add(chase); selector.Add(wander); selector.Add(idle);

        selector.Tick(null, 0.1f);

        Assert.Equal("wander", selector.CurrentName);
    }

    [Fact]
    public void NothingRunningMeansNone()
    {
        var selector = new GoalSelector();
        selector.Add(new FakeGoal("a", new List<string>()) { Wants = false });

        selector.Tick(null, 0.1f);

        Assert.Equal("none", selector.CurrentName);
    }

    [Fact]
    public void AnInterruptingGoalCutsInOnALowerOne()
    {
        var log = new List<string>();
        var chase = new FakeGoal("chase", log) { Wants = false, Interrupts = true };
        var idle = new FakeGoal("idle", log) { Wants = true };

        var selector = new GoalSelector();
        selector.Add(chase); selector.Add(idle);

        selector.Tick(null, 0.1f);
        Assert.Equal("idle", selector.CurrentName);

        chase.Wants = true;
        selector.Tick(null, 0.1f);

        Assert.Equal("chase", selector.CurrentName);
        Assert.Contains("stop idle", log);
    }

    [Fact]
    public void AGoalThatCannotInterruptWaitsItsTurn()
    {
        var log = new List<string>();
        var wander = new FakeGoal("wander", log) { Wants = false, Interrupts = false };
        var idle = new FakeGoal("idle", log) { Wants = true };

        var selector = new GoalSelector();
        selector.Add(wander); selector.Add(idle);

        selector.Tick(null, 0.1f);
        wander.Wants = true;
        selector.Tick(null, 0.1f);

        Assert.Equal("idle", selector.CurrentName); // still idling: wander only gets a turn when idle is done
    }

    [Fact]
    public void WhenAGoalEndsTheNextOneIsPickedAtOnce()
    {
        var log = new List<string>();
        var wander = new FakeGoal("wander", log) { Wants = true };
        var idle = new FakeGoal("idle", log) { Wants = true };

        var selector = new GoalSelector();
        selector.Add(wander); selector.Add(idle);

        selector.Tick(null, 0.1f);
        wander.Wants = false;
        wander.Keeps = false;
        selector.Tick(null, 0.1f);

        Assert.Equal("idle", selector.CurrentName);
        Assert.Equal(new[] { "start wander", "tick wander", "stop wander", "start idle", "tick idle" }, log);
    }
}
