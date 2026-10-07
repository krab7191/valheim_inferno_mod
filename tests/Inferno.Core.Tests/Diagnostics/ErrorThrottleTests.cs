using System;
using Inferno.Core.Diagnostics;
using Xunit;

namespace Inferno.Core.Tests.Diagnostics;

public class ErrorThrottleTests
{
    [Fact]
    public void FirstOccurrence_IsLoggedInFull()
    {
        var throttle = new ErrorThrottle(60);

        var decision = throttle.Record("a", 0);

        Assert.True(decision.Log);
        Assert.True(decision.FirstTime);
        Assert.Equal(0, decision.Suppressed);
    }

    [Fact]
    public void Repeats_AreSuppressedThenSummarised()
    {
        var throttle = new ErrorThrottle(60);
        throttle.Record("a", 0);

        var second = throttle.Record("a", 10);
        var third = throttle.Record("a", 20);
        var later = throttle.Record("a", 60);

        Assert.False(second.Log);
        Assert.Equal(1, second.Suppressed);
        Assert.False(third.Log);
        Assert.Equal(2, third.Suppressed);
        Assert.True(later.Log);
        Assert.False(later.FirstTime);
        Assert.Equal(2, later.Suppressed);

        var afterSummary = throttle.Record("a", 61);
        Assert.False(afterSummary.Log);
        Assert.Equal(1, afterSummary.Suppressed);
    }

    [Fact]
    public void Keys_AreIndependent_AndEverythingIsCounted()
    {
        var throttle = new ErrorThrottle(60);

        Assert.True(throttle.Record("a", 0).Log);
        Assert.True(throttle.Record("b", 0).Log);
        Assert.False(throttle.Record("a", 1).Log);

        Assert.Equal(3, throttle.Total);
        Assert.Equal(2, throttle.Distinct);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_InvalidInterval_Throws(double interval) =>
        Assert.Throws<ArgumentOutOfRangeException>("intervalSeconds", () => new ErrorThrottle(interval));

    [Fact]
    public void Record_NullKey_Throws() =>
        Assert.Throws<ArgumentNullException>("key", () => new ErrorThrottle(1).Record(null!, 0));
}
