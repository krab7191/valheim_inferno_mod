using Inferno.Core.Diagnostics;
using Xunit;

namespace Inferno.Core.Tests.Diagnostics;

public class RunningStatsTests
{
    [Fact]
    public void Empty_IsZero()
    {
        var stats = new RunningStats();

        Assert.Equal(0, stats.Count);
        Assert.Equal(0, stats.Average);
        Assert.Equal(0, stats.Max);
        Assert.Equal("0 × avg 0.00 ms, max 0.00 ms", stats.ToString());
    }

    [Fact]
    public void Add_TracksCountAverageAndMax()
    {
        var stats = new RunningStats();
        stats.Add(1);
        stats.Add(3);
        stats.Add(-1);              // ignored
        stats.Add(double.NaN);      // ignored
        stats.Add(double.PositiveInfinity); // ignored

        Assert.Equal(2, stats.Count);
        Assert.Equal(2, stats.Average);
        Assert.Equal(3, stats.Max);
        Assert.Equal("2 × avg 2.00 ms, max 3.00 ms", stats.ToString());
    }

    [Fact]
    public void Reset_StartsOver()
    {
        var stats = new RunningStats();
        stats.Add(5);

        stats.Reset();

        Assert.Equal(0, stats.Count);
        Assert.Equal(0, stats.Max);
    }
}
