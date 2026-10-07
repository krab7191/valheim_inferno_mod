using System;
using Inferno.Core.Commands;
using Xunit;

namespace Inferno.Core.Tests.Commands;

public class RateLimiterTests
{
    [Fact]
    public void Burst_ThenWarnOnce_ThenSilent()
    {
        var limiter = new RateLimiter(burst: 3, refillPerSecond: 1);

        Assert.Equal(RateDecision.Allow, limiter.TryTake("p", 0));
        Assert.Equal(RateDecision.Allow, limiter.TryTake("p", 0));
        Assert.Equal(RateDecision.Allow, limiter.TryTake("p", 0));
        Assert.Equal(RateDecision.DenyAndWarn, limiter.TryTake("p", 0));
        Assert.Equal(RateDecision.DenySilently, limiter.TryTake("p", 0.5));
        Assert.Equal(RateDecision.DenySilently, limiter.TryTake("p", 0.9));
    }

    [Fact]
    public void Refills_OverTime_AndWarnsAgainAfterRecovery()
    {
        var limiter = new RateLimiter(burst: 1, refillPerSecond: 2);

        Assert.Equal(RateDecision.Allow, limiter.TryTake("p", 0));
        Assert.Equal(RateDecision.DenyAndWarn, limiter.TryTake("p", 0.1));
        Assert.Equal(RateDecision.Allow, limiter.TryTake("p", 0.6));       // 0.5 s × 2/s = 1 token
        Assert.Equal(RateDecision.DenyAndWarn, limiter.TryTake("p", 0.6)); // warned again after a success
    }

    [Fact]
    public void Players_AreIndependent()
    {
        var limiter = new RateLimiter(burst: 1, refillPerSecond: 0.1);

        Assert.Equal(RateDecision.Allow, limiter.TryTake("a", 0));
        Assert.Equal(RateDecision.Allow, limiter.TryTake("b", 0));
        Assert.Equal(RateDecision.DenyAndWarn, limiter.TryTake("a", 0));
    }

    [Fact]
    public void RecoveredPlayers_AreForgotten()
    {
        var limiter = new RateLimiter(burst: 2, refillPerSecond: 1);
        limiter.TryTake("a", 0);
        limiter.TryTake("b", 0);
        Assert.Equal(2, limiter.Count);

        limiter.TryTake("c", 5); // a and b are full again by now

        Assert.Equal(1, limiter.Count);
    }

    [Fact]
    public void ClockGoingBackwards_DoesNotAddTokens()
    {
        var limiter = new RateLimiter(burst: 1, refillPerSecond: 1);

        Assert.Equal(RateDecision.Allow, limiter.TryTake("p", 10));
        Assert.Equal(RateDecision.DenyAndWarn, limiter.TryTake("p", 5));
    }

    [Fact]
    public void InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>("burst", () => new RateLimiter(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>("refillPerSecond", () => new RateLimiter(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>("refillPerSecond", () => new RateLimiter(1, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>("refillPerSecond", () => new RateLimiter(1, double.PositiveInfinity));
        Assert.Throws<ArgumentNullException>("playerKey", () => new RateLimiter(1, 1).TryTake(null!, 0));
    }
}
