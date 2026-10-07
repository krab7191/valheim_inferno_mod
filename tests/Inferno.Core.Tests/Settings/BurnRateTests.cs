using System;
using Inferno.Core.Settings;
using Xunit;

namespace Inferno.Core.Tests.Settings;

public class BurnRateTests
{
    [Theory]
    [InlineData(-10, 0.0)]
    [InlineData(-5, 0.5)]
    [InlineData(-1, 0.9)]
    [InlineData(0, 1.0)]
    [InlineData(1, 1.1)]
    [InlineData(5, 1.5)]
    [InlineData(10, 2.0)]
    public void ToMultiplier_EachStepIsTenPercent(int level, double expected) =>
        Assert.Equal(expected, BurnRate.ToMultiplier(level), precision: 12);

    [Theory]
    [InlineData(-11)]
    [InlineData(11)]
    public void ToMultiplier_OutOfRange_Throws(int level) =>
        Assert.Throws<ArgumentOutOfRangeException>("level", () => BurnRate.ToMultiplier(level));
}
