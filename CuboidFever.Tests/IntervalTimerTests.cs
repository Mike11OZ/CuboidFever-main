using System;
using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

public class IntervalTimerTests
{
    [Fact]
    public void FiresOnceTheIntervalHasPassed()
    {
        var timer = new IntervalTimer(10f);

        Assert.False(timer.Advance(4f));
        Assert.False(timer.Advance(5.9f));
        Assert.True(timer.Advance(0.2f));
    }

    [Fact]
    public void StartsAgainAfterFiring()
    {
        var timer = new IntervalTimer(2f);

        Assert.True(timer.Advance(2f));
        Assert.False(timer.Advance(1f));
        Assert.True(timer.Advance(1f));
    }

    [Fact]
    public void OneVeryLongFrame_FiresOnlyOnce()
    {
        var timer = new IntervalTimer(1f);

        Assert.True(timer.Advance(100f));
        Assert.False(timer.Advance(0.5f));
    }

    [Fact]
    public void Reset_StartsCountingFromZero()
    {
        var timer = new IntervalTimer(10f);
        timer.Advance(9f);

        timer.Reset();

        Assert.False(timer.Advance(2f));
    }

    [Fact]
    public void NegativeTime_IsIgnored_AndABadIntervalIsRejected()
    {
        var timer = new IntervalTimer(5f);

        Assert.False(timer.Advance(-3f));
        Assert.False(timer.Advance(4f));

        Assert.Throws<ArgumentOutOfRangeException>(() => new IntervalTimer(0f));
    }
}
