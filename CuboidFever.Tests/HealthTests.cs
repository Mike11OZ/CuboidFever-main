using CuboidFever;
using Xunit;

namespace CuboidFever.Tests;

public class HealthTests
{
    [Fact]
    public void StartsFull()
    {
        var health = new PlayerHealth();

        Assert.Equal(100f, health.Current);
        Assert.Equal(1f, health.Fraction);
        Assert.False(health.IsDead);
    }

    [Fact]
    public void DamageStopsAtZero_AndMeansDeath()
    {
        var health = new PlayerHealth();

        health.Damage(30f);
        Assert.Equal(70f, health.Current);

        health.Damage(500f);
        Assert.Equal(0f, health.Current);
        Assert.True(health.IsDead);
    }

    [Fact]
    public void HealStopsAtFull()
    {
        var health = new PlayerHealth();
        health.Damage(10f);

        health.Heal(50f);

        Assert.Equal(100f, health.Current);
    }

    [Fact]
    public void NonsenseAmountsAreIgnored()
    {
        var health = new PlayerHealth();

        health.Damage(-5f);
        health.Damage(float.NaN);
        health.Heal(-5f);

        Assert.Equal(100f, health.Current);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(3f, 0f)]      // the safe distance itself is free
    [InlineData(4f, 5f)]
    [InlineData(13f, 50f)]
    [InlineData(23f, 100f)]   // lethal
    [InlineData(-2f, 0f)]
    public void FallDamageGrowsWithHeight(float distance, float expected)
    {
        Assert.Equal(expected, FallDamage.Calculate(distance), 3);
    }

    [Fact]
    public void FallDamage_NaNIsHarmless()
    {
        Assert.Equal(0f, FallDamage.Calculate(float.NaN));
    }
}
